using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Mcp.Abstractions;
using Mcp.Bundles;
using Mcp.Runtime;
using Mcp.Policy;
using Mcp.Observability;

namespace Mcp.Server;

/// <summary>
/// Router principal del servidor MCP que maneja solicitudes JSON-RPC
/// </summary>
public class McpRouter
{
    private readonly ILogger<McpRouter> _logger;
    private readonly BundleLoader _bundleLoader;
    private readonly RuntimeManager _runtimeManager;
    private readonly IPolicyEngine _policyEngine;
    private readonly DiscoveryService _discoveryService;
    private readonly Dictionary<string, object> _sessions = new();

    public McpRouter(
        ILogger<McpRouter> logger,
        BundleLoader bundleLoader,
        RuntimeManager runtimeManager,
        IPolicyEngine policyEngine,
        DiscoveryService discoveryService)
    {
        _logger = logger;
        _bundleLoader = bundleLoader;
        _runtimeManager = runtimeManager;
        _policyEngine = policyEngine;
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Maneja una conexión WebSocket MCP
    /// </summary>
    public async Task HandleAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString();
        var buffer = new byte[4096];
        
        McpMetrics.ActiveWebSocketConnections.Inc();
        McpMetrics.WebSocketConnectionsTotal.WithLabels("connected").Inc();
        
        _logger.LogInformation("Nueva conexión WebSocket: {SessionId}", sessionId);

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var response = await ProcessMessageAsync(message, sessionId, cancellationToken);
                    
                    if (response != null)
                    {
                        var responseJson = JsonSerializer.Serialize(response);
                        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                        await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, cancellationToken);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en conexión WebSocket {SessionId}", sessionId);
        }
        finally
        {
            McpMetrics.ActiveWebSocketConnections.Dec();
            McpMetrics.WebSocketConnectionsTotal.WithLabels("disconnected").Inc();
            _sessions.Remove(sessionId);
            _logger.LogInformation("Conexión WebSocket cerrada: {SessionId}", sessionId);
        }
    }

    private async Task<JsonRpcResponse?> ProcessMessageAsync(string message, string sessionId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Procesando mensaje raw: {Message}", message);
            _logger.LogDebug("Longitud del mensaje: {Length} caracteres", message.Length);
            _logger.LogDebug("Bytes del mensaje: {Bytes}", string.Join(", ", System.Text.Encoding.UTF8.GetBytes(message).Take(50)));
            
            // Intentar parsear con opciones más permisivas
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(message, options);
            if (request == null)
            {
                _logger.LogError("No se pudo deserializar el mensaje JSON: {Message}", message);
                return CreateErrorResponse(McpConstants.ParseError, "Invalid JSON", "unknown");
            }

            // Logging detallado para debug
            _logger.LogInformation("Request deserializado - Method: '{Method}', Id: '{Id}', Params: {Params}", 
                request.Method ?? "NULL", 
                GetIdAsString(request.Id), 
                request.Params?.GetRawText() ?? "NULL");

            // Validar que el método no esté vacío
            if (string.IsNullOrWhiteSpace(request.Method))
            {
                _logger.LogError("Método vacío o nulo en la solicitud: '{Method}'", request.Method);
                return CreateErrorResponse(McpConstants.InvalidRequest, "Method cannot be empty", GetIdAsString(request.Id));
            }

            _logger.LogDebug("Procesando solicitud: {Method} {Id}", request.Method, GetIdAsString(request.Id));

            JsonRpcResponse response;
            switch (request.Method)
            {
                case McpConstants.Initialize:
                    response = await HandleInitializeAsync(request, sessionId);
                    break;
                    
                case McpConstants.ToolsList:
                    response = await HandleToolsListAsync(request);
                    break;
                    
                case McpConstants.ToolsCall:
                    response = await HandleToolsCallAsync(request, sessionId, cancellationToken);
                    break;
                    
                case McpConstants.ResourcesList:
                    response = await HandleResourcesListAsync(request);
                    break;
                    
                case McpConstants.ResourcesRead:
                    response = await HandleResourcesReadAsync(request);
                    break;
                    
                default:
                    response = CreateErrorResponse(McpConstants.MethodNotFound, $"Method '{request.Method}' not found", GetIdAsString(request.Id));
                    break;
            }

            stopwatch.Stop();
            
            // Registrar métricas
            var status = response.Error != null ? "error" : "success";
            var toolNamespace = "unknown";
            var toolName = "unknown";
            
            if (request.Method == McpConstants.ToolsCall && request.Params.HasValue)
            {
                try
                {
                    var toolParams = JsonSerializer.Deserialize<ToolInvokeParams>(request.Params.Value.GetRawText());
                    toolNamespace = toolParams?.Namespace ?? "unknown";
                    toolName = toolParams?.Name ?? "unknown";
                }
                catch { /* Ignorar errores de parsing */ }
            }
            
            // Asegurar que no hay valores nulos en las métricas
            var safeMethod = request.Method ?? "unknown";
            var safeStatus = status ?? "unknown";
            var safeToolNamespace = toolNamespace ?? "unknown";
            var safeToolName = toolName ?? "unknown";
            
            McpMetrics.RequestsTotal.WithLabels(safeMethod, safeStatus, safeToolNamespace, safeToolName).Inc();
            McpMetrics.RequestDuration.WithLabels(safeMethod, safeToolNamespace, safeToolName).Observe(stopwatch.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error procesando mensaje: {Message}", message);
            
            // Usar valores seguros para las métricas
            McpMetrics.RequestsTotal.WithLabels("unknown", "error", "unknown", "unknown").Inc();
            McpMetrics.RequestDuration.WithLabels("unknown", "unknown", "unknown").Observe(stopwatch.Elapsed.TotalSeconds);
            
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, "unknown");
        }
    }

    private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request, string sessionId)
    {
        try
        {
            var initParams = JsonSerializer.Deserialize<InitializeParams>(request.Params?.GetRawText() ?? "{}");
            if (initParams == null)
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Invalid initialize parameters", GetIdAsString(request.Id));
            }

            _logger.LogInformation("Inicializando sesión {SessionId} para cliente {ClientName}", sessionId, initParams.ClientName);

            var capabilities = new Capabilities(
                Features: _discoveryService.GetDescriptor().Capabilities,
                ProtocolVersion: McpConstants.ProtocolVersion,
                ServerInfo: JsonSerializer.SerializeToElement(new
                {
                    name = "MCP AutoServer",
                    version = "1.0.0",
                    capabilities = _discoveryService.GetDescriptor().Capabilities
                })
            );

            var result = JsonSerializer.SerializeToElement(capabilities);
            return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en initialize");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, GetIdAsString(request.Id));
        }
    }

    private async Task<JsonRpcResponse> HandleToolsListAsync(JsonRpcRequest request)
    {
        try
        {
            var tools = new List<ToolDescriptor>();
            
            foreach (var bundle in _bundleLoader.Bundles.Values)
            {
                foreach (var toolEntry in bundle.Tools)
                {
                    var tool = toolEntry.Value;
                    var toolKey = toolEntry.Key; // Esto será "meme.generate", "meme.templates", etc.
                    
                    // Extraer el nombre real de la herramienta (sin namespace)
                    var toolName = toolKey.Contains('.') ? toolKey.Split('.', 2)[1] : toolKey;
                    
                    // Construir paths completos para los schemas
                    JsonElement? inputSchema = null;
                    JsonElement? outputSchema = null;
                    
                    if (!string.IsNullOrEmpty(tool.InputSchema))
                    {
                        try
                        {
                            var inputSchemaPath = Path.Combine(bundle.BundlePath, tool.InputSchema);
                            if (File.Exists(inputSchemaPath))
                            {
                                var inputSchemaContent = await File.ReadAllTextAsync(inputSchemaPath);
                                inputSchema = JsonDocument.Parse(inputSchemaContent).RootElement.Clone();
                            }
                            else
                            {
                                _logger.LogWarning("Schema de entrada no encontrado: {Path}", inputSchemaPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error leyendo schema de entrada: {Path}", tool.InputSchema);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(tool.OutputSchema))
                    {
                        try
                        {
                            var outputSchemaPath = Path.Combine(bundle.BundlePath, tool.OutputSchema);
                            if (File.Exists(outputSchemaPath))
                            {
                                var outputSchemaContent = await File.ReadAllTextAsync(outputSchemaPath);
                                outputSchema = JsonDocument.Parse(outputSchemaContent).RootElement.Clone();
                            }
                            else
                            {
                                _logger.LogWarning("Schema de salida no encontrado: {Path}", outputSchemaPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error leyendo schema de salida: {Path}", tool.OutputSchema);
                        }
                    }
                    
                    tools.Add(new ToolDescriptor(
                        Namespace: bundle.Manifest.Namespace,
                        Name: toolName, // Usar el nombre extraído, no tool.Name
                        Version: bundle.Manifest.Version,
                        Description: tool.Metadata?.GetProperty("description").GetString(),
                        InputSchema: inputSchema,
                        OutputSchema: outputSchema
                    ));
                }
            }

            var result = JsonSerializer.SerializeToElement(new { tools });
            return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando herramientas");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, GetIdAsString(request.Id));
        }
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            // Parsear parámetros de manera más flexible
            var paramsElement = request.Params ?? JsonSerializer.SerializeToElement(new { });
            string namespaceName, toolName;
            JsonElement arguments;

            // Intentar parsear en formato { name: "namespace.tool", arguments: {...} }
            if (paramsElement.TryGetProperty("name", out var nameElement))
            {
                var fullName = nameElement.GetString();
                if (string.IsNullOrEmpty(fullName) || !fullName.Contains('.'))
                {
                    return CreateErrorResponse(McpConstants.InvalidParams, "Invalid tool name format. Expected 'namespace.tool'", GetIdAsString(request.Id));
                }
                
                var parts = fullName.Split('.', 2);
                namespaceName = parts[0];
                toolName = parts[1];
                
                if (!paramsElement.TryGetProperty("arguments", out arguments))
                {
                    arguments = JsonSerializer.SerializeToElement(new { });
                }
            }
            // Intentar parsear en formato { namespace: "...", name: "...", arguments: {...} }
            else if (paramsElement.TryGetProperty("namespace", out var namespaceElement) && 
                     paramsElement.TryGetProperty("name", out var toolNameElement))
            {
                namespaceName = namespaceElement.GetString() ?? "";
                toolName = toolNameElement.GetString() ?? "";
                
                if (!paramsElement.TryGetProperty("arguments", out arguments))
                {
                    arguments = JsonSerializer.SerializeToElement(new { });
                }
            }
            else
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Invalid tool call parameters. Expected 'name' or 'namespace' and 'name'", GetIdAsString(request.Id));
            }

            if (string.IsNullOrEmpty(namespaceName) || string.IsNullOrEmpty(toolName))
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Namespace and tool name cannot be empty", GetIdAsString(request.Id));
            }

            // Obtener especificación de la herramienta
            var toolSpec = _bundleLoader.GetTool(namespaceName, toolName);
            if (toolSpec == null)
            {
                return CreateErrorResponse(McpConstants.InvalidParams, $"Tool '{namespaceName}.{toolName}' not found", GetIdAsString(request.Id));
            }

            // Obtener la ruta del bundle para pasar al runtime
            var bundlePath = _bundleLoader.GetBundlePath(namespaceName);
            if (string.IsNullOrEmpty(bundlePath))
            {
                return CreateErrorResponse(McpConstants.InternalError, $"Bundle path not found for namespace '{namespaceName}'", GetIdAsString(request.Id));
            }

            // Evaluar políticas
            var policyContext = new PolicyContext(
                ClientId: sessionId,
                ToolNamespace: namespaceName,
                ToolName: toolName,
                Arguments: arguments,
                ToolSpec: toolSpec,
                Metadata: new Dictionary<string, object>()
            );

            var policyResult = await _policyEngine.EvaluateAsync(policyContext, cancellationToken);
            if (!policyResult.IsAllowed)
            {
                // Asegurar valores seguros para las métricas
                var policySafeNamespace = namespaceName ?? "unknown";
                var policySafeToolName = toolName ?? "unknown";
                McpMetrics.PolicyErrors.WithLabels(policySafeNamespace, policySafeToolName, "denied").Inc();
                return CreateErrorResponse(McpConstants.InvalidRequest, $"Policy denied: {policyResult.Reason}", GetIdAsString(request.Id));
            }

            // Ejecutar herramienta
            var executionStopwatch = Stopwatch.StartNew();
            var toolResult = await _runtimeManager.ExecuteToolAsync(toolSpec, arguments, bundlePath, cancellationToken);
            executionStopwatch.Stop();

            // Registrar métricas de ejecución con valores seguros
            var executionStatus = toolResult.IsError ? "error" : "success";
            var execSafeNamespace = namespaceName ?? "unknown";
            var execSafeToolName = toolName ?? "unknown";
            var execSafeRuntime = toolSpec.Runtime ?? "unknown";
            
            McpMetrics.ToolExecutionsTotal.WithLabels(execSafeNamespace, execSafeToolName, execSafeRuntime, executionStatus).Inc();
            McpMetrics.ToolExecutionDuration.WithLabels(execSafeNamespace, execSafeToolName, execSafeRuntime).Observe(executionStopwatch.Elapsed.TotalSeconds);

            if (toolResult.IsError)
            {
                return CreateErrorResponse(McpConstants.InternalError, toolResult.ErrorMessage ?? "Tool execution failed", GetIdAsString(request.Id));
            }

            return new JsonRpcResponse(McpConstants.JsonRpcVersion, toolResult.Content, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando herramienta");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, GetIdAsString(request.Id));
        }
    }

    private async Task<JsonRpcResponse> HandleResourcesListAsync(JsonRpcRequest request)
    {
        try
        {
            var resources = new List<ResourceDescriptor>();
            
            foreach (var bundle in _bundleLoader.Bundles.Values)
            {
                foreach (var resourceEntry in bundle.Resources)
                {
                    var resource = resourceEntry.Value;
                    var resourceKey = resourceEntry.Key; // Esto será "meme.templates", etc.
                    
                    // Extraer el nombre real del recurso (sin namespace)
                    var resourceName = resourceKey.Contains('.') ? resourceKey.Split('.', 2)[1] : resourceKey;
                    
                    // Construir path completo para el schema del recurso
                    JsonElement? schema = null;
                    
                    if (!string.IsNullOrEmpty(resource.Schema))
                    {
                        try
                        {
                            var schemaPath = Path.Combine(bundle.BundlePath, resource.Schema);
                            if (File.Exists(schemaPath))
                            {
                                var schemaContent = await File.ReadAllTextAsync(schemaPath);
                                schema = JsonDocument.Parse(schemaContent).RootElement.Clone();
                            }
                            else
                            {
                                _logger.LogWarning("Schema de recurso no encontrado: {Path}", schemaPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error leyendo schema de recurso: {Path}", resource.Schema);
                        }
                    }
                    
                    resources.Add(new ResourceDescriptor(
                        Namespace: bundle.Manifest.Namespace,
                        Name: resourceName, // Usar el nombre extraído, no resource.Name
                        Version: bundle.Manifest.Version,
                        Description: resource.Metadata?.GetProperty("description").GetString(),
                        Schema: schema
                    ));
                }
            }

            var result = JsonSerializer.SerializeToElement(new { resources });
            return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando recursos");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, GetIdAsString(request.Id));
        }
    }

    private async Task<JsonRpcResponse> HandleResourcesReadAsync(JsonRpcRequest request)
    {
        try
        {
            var paramsElement = request.Params ?? JsonSerializer.SerializeToElement(new { });
            
            if (!paramsElement.TryGetProperty("name", out var nameElement))
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Missing 'name' parameter", GetIdAsString(request.Id));
            }
            
            var fullName = nameElement.GetString();
            if (string.IsNullOrEmpty(fullName) || !fullName.Contains('.'))
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Invalid resource name format. Expected 'namespace.resource'", GetIdAsString(request.Id));
            }
            
            var parts = fullName.Split('.', 2);
            var namespaceName = parts[0];
            var resourceName = parts[1];
            
            // Buscar el recurso
            var resource = _bundleLoader.GetResource(namespaceName, resourceName);
            if (resource == null)
            {
                return CreateErrorResponse(McpConstants.InvalidParams, $"Resource '{fullName}' not found", GetIdAsString(request.Id));
            }
            
            // Leer el contenido del recurso
            try
            {
                var bundle = _bundleLoader.Bundles.Values.FirstOrDefault(b => b.Manifest.Namespace == namespaceName);
                if (bundle == null)
                {
                    return CreateErrorResponse(McpConstants.InternalError, "Bundle not found", GetIdAsString(request.Id));
                }
                
                var resourcePath = Path.Combine(bundle.BundlePath, resource.Schema);
                if (!File.Exists(resourcePath))
                {
                    return CreateErrorResponse(McpConstants.InternalError, $"Resource file not found: {resourcePath}", GetIdAsString(request.Id));
                }
                
                var resourceContent = await File.ReadAllTextAsync(resourcePath);
                var resourceJson = JsonDocument.Parse(resourceContent).RootElement.Clone();
                
                var result = JsonSerializer.SerializeToElement(new { content = resourceJson });
                return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo contenido del recurso: {Resource}", fullName);
                return CreateErrorResponse(McpConstants.InternalError, $"Error reading resource: {ex.Message}", GetIdAsString(request.Id));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en resources/read");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, GetIdAsString(request.Id));
        }
    }

    private static JsonRpcResponse CreateErrorResponse(int code, string message, string id)
    {
        var error = new JsonRpcError(code, message, null);
        return new JsonRpcResponse(McpConstants.JsonRpcVersion, null, error, JsonSerializer.SerializeToElement(id));
    }

    /// <summary>
    /// Convierte el ID de JsonElement a string de manera segura
    /// </summary>
    private static string GetIdAsString(JsonElement? idElement)
    {
        if (idElement == null) return "unknown";
        
        try
        {
            return idElement.Value.ValueKind switch
            {
                JsonValueKind.String => idElement.Value.GetString() ?? "unknown",
                JsonValueKind.Number => idElement.Value.GetInt32().ToString(),
                _ => "unknown"
            };
        }
        catch
        {
            return "unknown";
        }
    }
}

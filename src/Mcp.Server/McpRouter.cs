using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
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
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(message);
            if (request == null)
            {
                return CreateErrorResponse(McpConstants.ParseError, "Invalid JSON", request?.Id ?? "unknown");
            }

            _logger.LogDebug("Procesando solicitud: {Method} {Id}", request.Method, request.Id);

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
                    
                default:
                    response = CreateErrorResponse(McpConstants.MethodNotFound, $"Method '{request.Method}' not found", request.Id);
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
            
            McpMetrics.RequestsTotal.WithLabels(request.Method, status, toolNamespace, toolName).Inc();
            McpMetrics.RequestDuration.WithLabels(request.Method, toolNamespace, toolName).Observe(stopwatch.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error procesando mensaje: {Message}", message);
            
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
                return CreateErrorResponse(McpConstants.InvalidParams, "Invalid initialize parameters", request.Id);
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
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, request.Id);
        }
    }

    private async Task<JsonRpcResponse> HandleToolsListAsync(JsonRpcRequest request)
    {
        try
        {
            var tools = new List<ToolDescriptor>();
            
            foreach (var bundle in _bundleLoader.Bundles.Values)
            {
                foreach (var tool in bundle.Tools.Values)
                {
                    tools.Add(new ToolDescriptor(
                        Namespace: bundle.Manifest.Namespace,
                        Name: tool.Name,
                        Version: bundle.Manifest.Version,
                        Description: tool.Metadata?.GetProperty("description").GetString(),
                        InputSchema: tool.InputSchema != null ? JsonDocument.Parse(File.ReadAllText(tool.InputSchema)).RootElement.Clone() : null,
                        OutputSchema: tool.OutputSchema != null ? JsonDocument.Parse(File.ReadAllText(tool.OutputSchema)).RootElement.Clone() : null
                    ));
                }
            }

            var result = JsonSerializer.SerializeToElement(new { tools });
            return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando herramientas");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, request.Id);
        }
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var toolParams = JsonSerializer.Deserialize<ToolInvokeParams>(request.Params?.GetRawText() ?? "{}");
            if (toolParams == null)
            {
                return CreateErrorResponse(McpConstants.InvalidParams, "Invalid tool call parameters", request.Id);
            }

            // Obtener especificación de la herramienta
            var toolSpec = _bundleLoader.GetTool(toolParams.Namespace, toolParams.Name);
            if (toolSpec == null)
            {
                return CreateErrorResponse(McpConstants.InvalidParams, $"Tool '{toolParams.Namespace}.{toolParams.Name}' not found", request.Id);
            }

            // Evaluar políticas
            var policyContext = new PolicyContext(
                ClientId: sessionId,
                ToolNamespace: toolParams.Namespace,
                ToolName: toolParams.Name,
                Arguments: toolParams.Arguments,
                ToolSpec: toolSpec,
                Metadata: new Dictionary<string, object>()
            );

            var policyResult = await _policyEngine.EvaluateAsync(policyContext, cancellationToken);
            if (!policyResult.IsAllowed)
            {
                McpMetrics.PolicyErrors.WithLabels(toolParams.Namespace, toolParams.Name, "denied").Inc();
                return CreateErrorResponse(McpConstants.InvalidRequest, $"Policy denied: {policyResult.Reason}", request.Id);
            }

            // Ejecutar herramienta
            var executionStopwatch = Stopwatch.StartNew();
            var toolResult = await _runtimeManager.ExecuteToolAsync(toolSpec, toolParams.Arguments, cancellationToken);
            executionStopwatch.Stop();

            // Registrar métricas de ejecución
            var executionStatus = toolResult.IsError ? "error" : "success";
            McpMetrics.ToolExecutionsTotal.WithLabels(toolParams.Namespace, toolParams.Name, toolSpec.Runtime, executionStatus).Inc();
            McpMetrics.ToolExecutionDuration.WithLabels(toolParams.Namespace, toolParams.Name, toolSpec.Runtime).Observe(executionStopwatch.Elapsed.TotalSeconds);

            if (toolResult.IsError)
            {
                return CreateErrorResponse(McpConstants.InternalError, toolResult.ErrorMessage ?? "Tool execution failed", request.Id);
            }

            return new JsonRpcResponse(McpConstants.JsonRpcVersion, toolResult.Content, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando herramienta");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, request.Id);
        }
    }

    private async Task<JsonRpcResponse> HandleResourcesListAsync(JsonRpcRequest request)
    {
        try
        {
            var resources = new List<ResourceDescriptor>();
            
            foreach (var bundle in _bundleLoader.Bundles.Values)
            {
                foreach (var resource in bundle.Resources.Values)
                {
                    resources.Add(new ResourceDescriptor(
                        Namespace: bundle.Manifest.Namespace,
                        Name: resource.Name,
                        Version: bundle.Manifest.Version,
                        Description: resource.Metadata?.GetProperty("description").GetString(),
                        Schema: resource.Schema != null ? JsonDocument.Parse(File.ReadAllText(resource.Schema)).RootElement.Clone() : null
                    ));
                }
            }

            var result = JsonSerializer.SerializeToElement(new { resources });
            return new JsonRpcResponse(McpConstants.JsonRpcVersion, result, null, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando recursos");
            return CreateErrorResponse(McpConstants.InternalError, ex.Message, request.Id);
        }
    }

    private static JsonRpcResponse CreateErrorResponse(int code, string message, string id)
    {
        var error = new JsonRpcError(code, message, null);
        return new JsonRpcResponse(McpConstants.JsonRpcVersion, null, error, id);
    }
}

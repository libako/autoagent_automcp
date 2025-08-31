using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Net.WebSockets;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Prometheus;
using Mcp.Bundles;
using Mcp.Runtime;
using Mcp.Policy;
using Mcp.Server;
using Mcp.Observability;

var builder = WebApplication.CreateBuilder(args);

// Configuración
builder.Services.Configure<BundleLoaderOptions>(builder.Configuration.GetSection("BundleLoader"));
builder.Services.Configure<DiscoveryOptions>(builder.Configuration.GetSection("Discovery"));
builder.Services.Configure<SimplePolicyEngineOptions>(builder.Configuration.GetSection("PolicyEngine"));

// Servicios de bundles
builder.Services.AddSingleton<BundleLoader>();
builder.Services.AddHostedService<BundleLoader>(provider => provider.GetRequiredService<BundleLoader>());

// Servicios de runtime
builder.Services.AddSingleton<SubprocessRuntime>();
builder.Services.AddSingleton<WasmRuntime>();
builder.Services.AddSingleton<IToolRuntime, SubprocessRuntime>();
builder.Services.AddSingleton<IToolRuntime, WasmRuntime>();
builder.Services.AddSingleton<RuntimeManager>();

// Servicios de políticas
builder.Services.AddSingleton<IPolicyEngine, SimplePolicyEngine>();

// Servicios de descubrimiento
builder.Services.AddSingleton<DiscoveryService>();
builder.Services.AddHostedService<DiscoveryService>(provider => provider.GetRequiredService<DiscoveryService>());

// Servicios del servidor
builder.Services.AddSingleton<McpRouter>();

// Autenticación (opcional) - COMENTADO PARA DEBUG
// builder.Services.AddAuthentication()
//     .AddJwtBearer(options =>
//     {
//         // Configurar JWT si es necesario
//         options.RequireHttpsMetadata = false;
//         options.SaveToken = true;
//     });

// builder.Services.AddAuthorization();

// Rate limiting - COMENTADO PARA DEBUG
// builder.Services.AddRateLimiter(options =>
// {
//     options.AddFixedWindowLimiter("ws", limiterOptions =>
//     {
//         limiterOptions.PermitLimit = 200;
//         limiterOptions.Window = TimeSpan.FromMinutes(1);
//         limiterOptions.QueueLimit = 0;
//     });
// });

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("McpRouter")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("McpMetrics")
        .AddPrometheusExporter());

var app = builder.Build();

// Habilitar WebSocket explícitamente
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
    ReceiveBufferSize = 4 * 1024
});

// Configurar pipeline HTTP (comentado temporalmente para debug)
// app.UseAuthentication();
// app.UseAuthorization();
// app.UseRateLimiter();

// Endpoint de descubrimiento .well-known/mcp
app.MapGet("/.well-known/mcp", (DiscoveryService discoveryService) =>
{
    var descriptor = discoveryService.GetDescriptor();
    return Results.Json(descriptor);
});

// Endpoint de métricas Prometheus
app.MapPrometheusScrapingEndpoint();

// Endpoint de estado
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Endpoint de prueba sin autenticación
app.MapGet("/test", () => Results.Ok(new { message = "¡Servidor funcionando!", timestamp = DateTime.UtcNow }));

// Endpoint de debug para runtimes
app.MapGet("/debug/runtimes", (RuntimeManager runtimeManager) =>
{
    var runtimes = runtimeManager.GetAvailableRuntimes();
    return Results.Json(new
    {
        availableRuntimes = runtimes,
        count = runtimes.Count,
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

// Endpoint de debug para herramientas
app.MapGet("/debug/tools/{namespace}.{tool}", (string namespaceName, string toolName, BundleLoader bundleLoader, RuntimeManager runtimeManager) =>
{
    var tool = bundleLoader.GetTool(namespaceName, toolName);
    if (tool == null)
    {
        return Results.NotFound(new { error = $"Herramienta {namespaceName}.{toolName} no encontrada" });
    }

    var canExecute = false;
    var compatibleRuntimes = new List<string>();
    
    foreach (var runtime in runtimeManager.GetAvailableRuntimes())
    {
        // Aquí necesitaríamos verificar si el runtime puede ejecutar la herramienta
        // Por ahora, solo verificamos si el runtime es compatible con el tipo
        if (runtime.Key == "subprocess" && (tool.Runtime == "node18" || tool.Runtime == "python3"))
        {
            canExecute = true;
            compatibleRuntimes.Add(runtime.Key);
        }
        else if (runtime.Key == "wasm" && tool.Runtime == "wasm")
        {
            canExecute = true;
            compatibleRuntimes.Add(runtime.Key);
        }
    }

    return Results.Json(new
    {
        tool = new
        {
            name = $"{namespaceName}.{toolName}",
            @namespace = namespaceName,
            tool = toolName,
            runtime = tool.Runtime,
            entry = tool.Entry,
            description = tool.Metadata?.GetProperty("description").GetString() ?? "Sin descripción",
            hasInputSchema = !string.IsNullOrEmpty(tool.InputSchema),
            hasOutputSchema = !string.IsNullOrEmpty(tool.OutputSchema)
        },
        canExecute = canExecute,
        compatibleRuntimes = compatibleRuntimes,
        availableRuntimes = runtimeManager.GetAvailableRuntimes(),
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

// Endpoints HTTP REST para herramientas (sin autenticación para pruebas)
app.MapGet("/tools", (BundleLoader bundleLoader) =>
{
    var tools = new List<object>();
    foreach (var bundle in bundleLoader.Bundles.Values)
    {
        foreach (var tool in bundle.Tools.Values)
        {
            tools.Add(new
            {
                name = $"{bundle.Manifest.Namespace}.{tool.Name}",
                @namespace = bundle.Manifest.Namespace,
                tool = tool.Name,
                description = tool.Metadata?.GetProperty("description").GetString() ?? "Sin descripción",
                runtime = tool.Runtime,
                bundle = bundle.Manifest.Name
            });
        }
    }
    return Results.Json(tools);
}).AllowAnonymous();

app.MapPost("/tools/{namespace}.{tool}:invoke", async (string namespaceName, string toolName, HttpContext context, BundleLoader bundleLoader, RuntimeManager runtimeManager) =>
{
    try
    {
        // Obtener la herramienta
        var tool = bundleLoader.GetTool(namespaceName, toolName);
        if (tool == null)
        {
            return Results.NotFound(new { error = $"Herramienta {namespaceName}.{toolName} no encontrada" });
        }

        // Obtener la ruta del bundle para pasar al runtime
        var bundlePath = bundleLoader.GetBundlePath(namespaceName);
        if (string.IsNullOrEmpty(bundlePath))
        {
            return Results.BadRequest(new { error = $"Bundle path not found for namespace '{namespaceName}'" });
        }

        // Leer el cuerpo de la solicitud
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        
        JsonElement arguments;
        if (string.IsNullOrEmpty(requestBody))
        {
            arguments = JsonSerializer.SerializeToElement(new { });
        }
        else
        {
            arguments = JsonSerializer.Deserialize<JsonElement>(requestBody);
        }

        // Ejecutar la herramienta
        var result = await runtimeManager.ExecuteToolAsync(tool, arguments, bundlePath, context.RequestAborted);
        
        return Results.Json(new
        {
            success = true,
            result = result,
            tool = $"{namespaceName}.{toolName}",
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = ex.Message,
            tool = $"{namespaceName}.{toolName}",
            timestamp = DateTime.UtcNow
        });
    }
}).AllowAnonymous();

// Endpoint WebSocket de prueba simple
app.Map("/ws-test", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[1024];
    
    while (webSocket.State == WebSocketState.Open)
    {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
        
        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            var response = $"{{\"echo\": \"{message}\", \"timestamp\": \"{DateTime.UtcNow:O}\"}}";
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, context.RequestAborted);
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", context.RequestAborted);
            break;
        }
    }
})
.AllowAnonymous();

// Endpoint WebSocket MCP (sin autenticación para pruebas)
app.Map("/ws", async (HttpContext context, McpRouter router) =>
{
    try
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Solicitud WebSocket recibida en /ws");
        logger.LogInformation("Headers: {Headers}", string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
        
        // Verificar manualmente si es una solicitud WebSocket
        var isWebSocketRequest = context.Request.Headers.ContainsKey("Upgrade") &&
                                context.Request.Headers["Upgrade"].ToString().ToLower() == "websocket" &&
                                context.Request.Headers.ContainsKey("Connection") &&
                                context.Request.Headers["Connection"].ToString().ToLower().Contains("upgrade");
        
        if (!isWebSocketRequest)
        {
            logger.LogWarning("No es una solicitud WebSocket válida (verificación manual)");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Bad Request: WebSocket upgrade required");
            return;
        }

        logger.LogInformation("Aceptando conexión WebSocket...");
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Conexión WebSocket aceptada, iniciando manejo...");
        await router.HandleAsync(webSocket, context.RequestAborted);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error en endpoint WebSocket /ws");
        
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Internal Server Error: {ex.Message}");
        }
    }
})
.AllowAnonymous();

// Cargar políticas iniciales
var policyEngine = app.Services.GetRequiredService<IPolicyEngine>();
var policyOptions = app.Services.GetRequiredService<IOptions<SimplePolicyEngineOptions>>();
await policyEngine.LoadPoliciesAsync(policyOptions.Value.DefaultPolicyPath);

// Actualizar métricas de bundles
var bundleLoader = app.Services.GetRequiredService<BundleLoader>();

// Actualizar métricas cada 30 segundos
_ = Task.Run(async () =>
{
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        try
        {
            var bundles = bundleLoader.Bundles;
            // McpMetrics.ActiveBundles.Set(bundles.Count);
            
            var runtimeManager = app.Services.GetRequiredService<RuntimeManager>();
            var runtimes = runtimeManager.GetAvailableRuntimes();
            
            // foreach (var runtime in runtimes)
            // {
            //     McpMetrics.ActiveTools.WithLabels(runtime.Key).Set(1);
            // }
            
            await Task.Delay(TimeSpan.FromSeconds(30), app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error actualizando métricas");
        }
    }
});

app.Logger.LogInformation("MCP AutoServer iniciando en puerto {Port}", 
    app.Services.GetRequiredService<IOptions<DiscoveryOptions>>().Value.Port);

await app.RunAsync();

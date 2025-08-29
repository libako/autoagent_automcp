using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zeroconf;
using System.Text.Json;
using Mcp.Bundles;

namespace Mcp.Server;

/// <summary>
/// Configuración del servicio de descubrimiento
/// </summary>
public class DiscoveryOptions
{
    public string ServiceName { get; set; } = "MCP AutoServer";
    public int Port { get; set; } = 8970;
    public string ProtocolVersion { get; set; } = "1.1";
    public string[] Capabilities { get; set; } = { "tools", "resources", "events" };
    public string[] AuthMethods { get; set; } = { "oidc", "apikey", "mtls" };
    public bool EnableMdns { get; set; } = true;
    public string[] AutoDiscoveryPaths { get; set; } = {
        "./bundles",
        "../../bundles", 
        "%USERPROFILE%/.mcp/bundles",
        "%PROGRAMDATA%/MCP/bundles",
        "/usr/local/share/mcp/bundles",
        "/opt/mcp/bundles"
    };
}

/// <summary>
/// Descriptor del servidor MCP para .well-known/mcp
/// </summary>
public record McpDescriptor(
    string Protocol,
    string Version,
    string[] Capabilities,
    string[] AuthMethods,
    Dictionary<string, string> Endpoints,
    JsonElement? Metadata = null,
    JsonElement? Tools = null
);

/// <summary>
/// Servicio de autodescubrimiento usando mDNS y .well-known
/// </summary>
public class DiscoveryService : BackgroundService
{
    private readonly ILogger<DiscoveryService> _logger;
    private readonly DiscoveryOptions _options;
    private readonly BundleLoader _bundleLoader;
    private IDisposable? _mdnsRegistration;

    public DiscoveryService(
        ILogger<DiscoveryService> logger, 
        IOptions<DiscoveryOptions> options,
        BundleLoader bundleLoader)
    {
        _logger = logger;
        _options = options.Value;
        _bundleLoader = bundleLoader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.EnableMdns)
        {
            await RegisterMdnsServiceAsync(stoppingToken);
        }

        _logger.LogInformation("Servicio de descubrimiento iniciado en puerto {Port}", _options.Port);
        _logger.LogInformation("Autodescubrimiento habilitado en {PathCount} ubicaciones", _options.AutoDiscoveryPaths.Length);

        // Mantener el servicio ejecutándose
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RegisterMdnsServiceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Simulación de registro mDNS - en una implementación real se usaría Zeroconf
            _logger.LogInformation("Simulando registro mDNS para: {ServiceName} en puerto {Port}", 
                _options.ServiceName, _options.Port);
            
            // En una implementación real, aquí se registraría el servicio mDNS
            // usando la API de Zeroconf apropiada
            
            await Task.Delay(100, cancellationToken); // Simulación de operación asíncrona
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando servicio mDNS");
        }
    }

    public override void Dispose()
    {
        _mdnsRegistration?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Obtiene el descriptor del servidor MCP con herramientas incluidas
    /// </summary>
    public McpDescriptor GetDescriptor()
    {
        var tools = GetAvailableTools();
        var discoveredPaths = GetDiscoveredPaths();
        
        return new McpDescriptor(
            Protocol: "mcp",
            Version: _options.ProtocolVersion,
            Capabilities: _options.Capabilities,
            AuthMethods: _options.AuthMethods,
            Endpoints: new Dictionary<string, string>
            {
                ["ws"] = $"ws://localhost:{_options.Port}/ws",
                ["http"] = $"http://localhost:{_options.Port}"
            },
            Metadata: JsonSerializer.SerializeToElement(new
            {
                name = _options.ServiceName,
                description = "MCP AutoServer - Servidor autodescubrible de herramientas",
                vendor = "MCP AutoServer Project",
                totalTools = tools.Count,
                totalBundles = _bundleLoader.Bundles.Count,
                discoveredPaths = discoveredPaths,
                autoDiscoveryEnabled = true
            }),
            Tools: JsonSerializer.SerializeToElement(tools)
        );
    }

    /// <summary>
    /// Obtiene la lista de herramientas disponibles
    /// </summary>
    private List<object> GetAvailableTools()
    {
        var tools = new List<object>();
        
        foreach (var bundle in _bundleLoader.Bundles.Values)
        {
            foreach (var tool in bundle.Tools.Values)
            {
                var toolInfo = new
                {
                    name = $"{bundle.Manifest.Namespace}.{tool.Name}",
                    @namespace = bundle.Manifest.Namespace,
                    tool = tool.Name,
                    version = bundle.Manifest.Version,
                    description = tool.Metadata?.GetProperty("description").GetString() ?? "Sin descripción",
                    runtime = tool.Runtime,
                    bundle = bundle.Manifest.Name,
                    bundlePath = bundle.BundlePath,
                    hasInputSchema = !string.IsNullOrEmpty(tool.InputSchema),
                    hasOutputSchema = !string.IsNullOrEmpty(tool.OutputSchema),
                    discoveredAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                
                tools.Add(toolInfo);
            }
        }
        
        return tools;
    }

    /// <summary>
    /// Obtiene las rutas donde se han descubierto herramientas
    /// </summary>
    private List<string> GetDiscoveredPaths()
    {
        var paths = new List<string>();
        
        foreach (var path in _options.AutoDiscoveryPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (Directory.Exists(expandedPath))
            {
                paths.Add(expandedPath);
            }
        }
        
        return paths;
    }
}

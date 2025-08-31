using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.Bundles;

/// <summary>
/// Configuración del cargador de bundles
/// </summary>
public class BundleLoaderOptions
{
    public string BundlesPath { get; set; } = "bundles";
    public string[] AutoDiscoveryPaths { get; set; } = {
        "./bundles",
        "../../bundles", 
        "%USERPROFILE%/.mcp/bundles",
        "%PROGRAMDATA%/MCP/bundles",
        "/usr/local/share/mcp/bundles",
        "/opt/mcp/bundles"
    };
    public bool EnableHotReload { get; set; } = true;
    public int ReloadDelayMs { get; set; } = 1000;
}

/// <summary>
/// Información de un bundle cargado
/// </summary>
public record LoadedBundle(
    string Id,
    BundleManifest Manifest,
    string BundlePath,
    DateTime LoadedAt,
    Dictionary<string, ToolSpec> Tools,
    Dictionary<string, ResourceSpec> Resources
);

/// <summary>
/// Cargador de bundles con hot-reload y autodescubrimiento
/// </summary>
public class BundleLoader : BackgroundService
{
    private readonly ILogger<BundleLoader> _logger;
    private readonly BundleLoaderOptions _options;
    private readonly ConcurrentDictionary<string, LoadedBundle> _bundles = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);

    public BundleLoader(ILogger<BundleLoader> logger, IOptions<BundleLoaderOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        // Configurar autodescubrimiento
        SetupAutoDiscovery();
        
        if (_options.EnableHotReload)
        {
            SetupHotReload();
        }
    }

    /// <summary>
    /// Configura el autodescubrimiento en múltiples directorios
    /// </summary>
    private void SetupAutoDiscovery()
    {
        var allPaths = new List<string> { _options.BundlesPath };
        allPaths.AddRange(_options.AutoDiscoveryPaths);
        
        foreach (var path in allPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (Directory.Exists(expandedPath))
            {
                _logger.LogInformation("Directorio de autodescubrimiento: {Path}", expandedPath);
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(expandedPath);
                    _logger.LogInformation("Directorio de autodescubrimiento creado: {Path}", expandedPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo crear directorio de autodescubrimiento: {Path}", expandedPath);
                }
            }
        }
    }

    /// <summary>
    /// Configura el hot-reload para todos los directorios
    /// </summary>
    private void SetupHotReload()
    {
        var allPaths = new List<string> { _options.BundlesPath };
        allPaths.AddRange(_options.AutoDiscoveryPaths);
        
        foreach (var path in allPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (Directory.Exists(expandedPath))
            {
                try
                {
                    var watcher = new FileSystemWatcher(expandedPath)
                    {
                        IncludeSubdirectories = true,
                        Filter = "bundle.json",
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
                    };
                    
                    watcher.Changed += OnBundleChanged;
                    watcher.Created += OnBundleChanged;
                    watcher.Deleted += OnBundleDeleted;
                    watcher.EnableRaisingEvents = true;
                    
                    _watchers.Add(watcher);
                    _logger.LogDebug("Hot-reload configurado para: {Path}", expandedPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo configurar hot-reload para: {Path}", expandedPath);
                }
            }
        }
    }

    /// <summary>
    /// Obtiene todos los bundles cargados
    /// </summary>
    public IReadOnlyDictionary<string, LoadedBundle> Bundles => _bundles;

    /// <summary>
    /// Obtiene la ruta de un bundle por su namespace
    /// </summary>
    public string? GetBundlePath(string namespaceName)
    {
        if (_bundles.TryGetValue(namespaceName, out var bundle))
        {
            return bundle.BundlePath;
        }
        return null;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando cargador de bundles desde {BundlesPath}", _options.BundlesPath);
        
        // Cargar bundles iniciales
        await LoadAllBundlesAsync();
        
        _logger.LogInformation("Cargados {Count} bundles", _bundles.Count);
        
        if (_options.EnableHotReload)
        {
            _logger.LogInformation("Hot-reload habilitado para bundles");
        }

        // Mantener el servicio ejecutándose
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>
    /// Carga todos los bundles de todos los directorios de autodescubrimiento
    /// </summary>
    private async Task LoadAllBundlesAsync()
    {
        var allPaths = new List<string> { _options.BundlesPath };
        allPaths.AddRange(_options.AutoDiscoveryPaths);
        
        foreach (var path in allPaths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (Directory.Exists(expandedPath))
            {
                await LoadBundlesFromDirectoryAsync(expandedPath);
            }
        }
    }

    /// <summary>
    /// Carga bundles desde un directorio específico
    /// </summary>
    private async Task LoadBundlesFromDirectoryAsync(string directoryPath)
    {
        try
        {
            var bundleDirectories = Directory.GetDirectories(directoryPath);
            
            foreach (var bundleDir in bundleDirectories)
            {
                var manifestPath = Path.Combine(bundleDir, "bundle.json");
                if (File.Exists(manifestPath))
                {
                    await LoadBundleAsync(bundleDir, manifestPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando bundles desde: {Directory}", directoryPath);
        }
    }

    /// <summary>
    /// Carga un bundle específico
    /// </summary>
    private async Task LoadBundleAsync(string bundlePath, string manifestPath)
    {
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<BundleManifest>(manifestJson);
            
            if (manifest == null)
            {
                _logger.LogWarning("Manifest inválido en: {Path}", manifestPath);
                return;
            }

            // Validar el bundle (validación básica del manifest)
            if (string.IsNullOrEmpty(manifest.Namespace) || string.IsNullOrEmpty(manifest.Name))
            {
                _logger.LogWarning("Bundle inválido: namespace o nombre vacío en {Path}", manifestPath);
                return;
            }

            // Crear diccionarios de herramientas y recursos
            var tools = new Dictionary<string, ToolSpec>();
            var resources = new Dictionary<string, ResourceSpec>();

            foreach (var tool in manifest.Tools)
            {
                var toolKey = $"{manifest.Namespace}.{tool.Name}";
                tools[toolKey] = tool;
            }

            foreach (var resource in manifest.Resources)
            {
                var resourceKey = $"{manifest.Namespace}.{resource.Name}";
                resources[resourceKey] = resource;
            }

            var loadedBundle = new LoadedBundle(
                Id: manifest.Namespace,
                Manifest: manifest,
                BundlePath: bundlePath,
                LoadedAt: DateTime.UtcNow,
                Tools: tools,
                Resources: resources
            );

            _bundles.AddOrUpdate(manifest.Namespace, loadedBundle, (key, old) => loadedBundle);
            
            _logger.LogInformation("Bundle cargado: {Bundle} ({Tools} herramientas, {Resources} recursos) desde {Path}", 
                manifest.Name, tools.Count, resources.Count, bundlePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando bundle desde: {Path}", bundlePath);
        }
    }

    private async void OnBundleChanged(object sender, FileSystemEventArgs e)
    {
        await _reloadSemaphore.WaitAsync();
        try
        {
            await Task.Delay(_options.ReloadDelayMs); // Evitar múltiples recargas
            
            var bundleDir = Path.GetDirectoryName(e.FullPath);
            if (bundleDir != null)
            {
                _logger.LogInformation("Bundle modificado, recargando: {Path}", bundleDir);
                await LoadBundleAsync(bundleDir, e.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en hot-reload");
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private async void OnBundleDeleted(object sender, FileSystemEventArgs e)
    {
        await _reloadSemaphore.WaitAsync();
        try
        {
            var bundleDir = Path.GetDirectoryName(e.FullPath);
            if (bundleDir != null)
            {
                var bundleName = Path.GetFileName(bundleDir);
                if (_bundles.TryRemove(bundleName, out var removedBundle))
                {
                    _logger.LogInformation("Bundle eliminado: {Bundle}", removedBundle.Manifest.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando bundle");
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene una herramienta específica por namespace y nombre
    /// </summary>
    public ToolSpec? GetTool(string namespaceName, string toolName)
    {
        if (_bundles.TryGetValue(namespaceName, out var bundle))
        {
            var toolKey = $"{namespaceName}.{toolName}";
            if (bundle.Tools.TryGetValue(toolKey, out var tool))
            {
                return tool;
            }
        }
        return null;
    }

    /// <summary>
    /// Obtiene un recurso específico por namespace y nombre
    /// </summary>
    public ResourceSpec? GetResource(string namespaceName, string resourceName)
    {
        if (_bundles.TryGetValue(namespaceName, out var bundle))
        {
            var resourceKey = $"{namespaceName}.{resourceName}";
            if (bundle.Resources.TryGetValue(resourceKey, out var resource))
            {
                return resource;
            }
        }
        return null;
    }

    public override void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher?.Dispose();
        }
        _reloadSemaphore?.Dispose();
        base.Dispose();
    }
}

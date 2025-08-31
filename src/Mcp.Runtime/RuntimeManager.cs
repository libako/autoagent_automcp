using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mcp.Bundles;

namespace Mcp.Runtime;

/// <summary>
/// Gestor de runtimes para coordinar la ejecución de herramientas
/// </summary>
public class RuntimeManager
{
    private readonly ILogger<RuntimeManager> _logger;
    private readonly ConcurrentDictionary<string, IToolRuntime> _runtimes = new();

    public RuntimeManager(ILogger<RuntimeManager> logger, IEnumerable<IToolRuntime> runtimes)
    {
        _logger = logger;
        
        foreach (var runtime in runtimes)
        {
            _runtimes[runtime.RuntimeName] = runtime;
            _logger.LogInformation("Runtime registrado: {RuntimeName}", runtime.RuntimeName);
        }
    }

    /// <summary>
    /// Ejecuta una herramienta usando el runtime apropiado
    /// </summary>
    public async Task<ToolInvokeResult> ExecuteToolAsync(ToolSpec tool, JsonElement arguments, string? bundlePath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Ejecutando herramienta: {ToolName} con runtime {Runtime}", tool.Name, tool.Runtime);

        // Buscar runtime que pueda ejecutar esta herramienta
        IToolRuntime? selectedRuntime = null;
        foreach (var runtime in _runtimes.Values)
        {
            if (runtime.CanExecute(tool))
            {
                selectedRuntime = runtime;
                break;
            }
        }

        if (selectedRuntime == null)
        {
            var availableRuntimes = string.Join(", ", _runtimes.Keys);
            var error = $"No se encontró un runtime compatible para {tool.Runtime}. Runtimes disponibles: {availableRuntimes}";
            _logger.LogError(error);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: error
            );
        }

        try
        {
            // Si el runtime soporta bundlePath, pasarlo
            if (selectedRuntime is SubprocessRuntime subprocessRuntime && !string.IsNullOrEmpty(bundlePath))
            {
                var result = await subprocessRuntime.InvokeAsync(tool, arguments, bundlePath, cancellationToken);
                
                if (result.IsError)
                {
                    _logger.LogError("Error ejecutando herramienta {ToolName}: {Error}", tool.Name, result.ErrorMessage);
                }
                else
                {
                    _logger.LogDebug("Herramienta {ToolName} ejecutada exitosamente en {ElapsedMs}ms", 
                        tool.Name, result.ExecutionTime.TotalMilliseconds);
                }
                
                return result;
            }
            else
            {
                var result = await selectedRuntime.InvokeAsync(tool, arguments, cancellationToken);
                
                if (result.IsError)
                {
                    _logger.LogError("Error ejecutando herramienta {ToolName}: {Error}", tool.Name, result.ErrorMessage);
                }
                else
                {
                    _logger.LogDebug("Herramienta {ToolName} ejecutada exitosamente en {ElapsedMs}ms", 
                        tool.Name, result.ExecutionTime.TotalMilliseconds);
                }
                
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción ejecutando herramienta {ToolName}", tool.Name);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// Obtiene información sobre los runtimes disponibles
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAvailableRuntimes()
    {
        return _runtimes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetType().Name);
    }

    /// <summary>
    /// Verifica si un runtime específico está disponible
    /// </summary>
    public bool HasRuntime(string runtimeName)
    {
        return _runtimes.ContainsKey(runtimeName);
    }
}

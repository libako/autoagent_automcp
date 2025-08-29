using System.Text.Json;
using Mcp.Bundles;

namespace Mcp.Runtime;

/// <summary>
/// Interfaz para ejecutar herramientas en diferentes runtimes
/// </summary>
public interface IToolRuntime
{
    /// <summary>
    /// Ejecuta una herramienta con los argumentos proporcionados
    /// </summary>
    Task<ToolInvokeResult> InvokeAsync(ToolSpec tool, JsonElement arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica si este runtime puede ejecutar la herramienta especificada
    /// </summary>
    bool CanExecute(ToolSpec tool);
    
    /// <summary>
    /// Nombre del runtime
    /// </summary>
    string RuntimeName { get; }
}

/// <summary>
/// Resultado de la ejecución de una herramienta
/// </summary>
public record ToolInvokeResult(
    JsonElement Content,
    bool IsError = false,
    string? ErrorMessage = null,
    TimeSpan ExecutionTime = default
);

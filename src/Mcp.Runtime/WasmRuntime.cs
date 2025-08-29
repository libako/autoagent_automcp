using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mcp.Bundles;
using Wasmtime;

namespace Mcp.Runtime;

/// <summary>
/// Runtime para ejecutar herramientas en WebAssembly usando Wasmtime
/// </summary>
public class WasmRuntime : IToolRuntime
{
    private readonly ILogger<WasmRuntime> _logger;

    public WasmRuntime(ILogger<WasmRuntime> logger)
    {
        _logger = logger;
    }

    public string RuntimeName => "wasm";

    public bool CanExecute(ToolSpec tool)
    {
        return tool.Runtime == RuntimeTypes.Wasm;
    }

    public async Task<ToolInvokeResult> InvokeAsync(ToolSpec tool, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var wasmPath = Path.Combine(tool.Entry);
            if (!Path.IsPathRooted(wasmPath))
            {
                wasmPath = Path.GetFullPath(wasmPath);
            }

            if (!File.Exists(wasmPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo WASM: {wasmPath}");
            }

            _logger.LogDebug("Ejecutando herramienta WASM: {WasmPath}", wasmPath);

            // Implementación simplificada para demostración
            // En una implementación real, se usaría la API completa de Wasmtime
            var inputJson = arguments.GetRawText();
            
            // Simular procesamiento WASM
            await Task.Delay(100, cancellationToken); // Simular tiempo de procesamiento
            
            var output = $"{{\"result\": \"WASM tool executed\", \"input\": {inputJson}}}";

            stopwatch.Stop();

            try
            {
                var result = JsonDocument.Parse(output).RootElement.Clone();
                _logger.LogDebug("Herramienta WASM ejecutada exitosamente en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                
                return new ToolInvokeResult(result, ExecutionTime: stopwatch.Elapsed);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parseando salida JSON de la herramienta WASM: {Output}", output);
                
                return new ToolInvokeResult(
                    JsonDocument.Parse("{}").RootElement.Clone(),
                    IsError: true,
                    ErrorMessage: $"Salida JSON inválida: {ex.Message}",
                    ExecutionTime: stopwatch.Elapsed
                );
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Ejecución de herramienta WASM cancelada después de {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: "Ejecución cancelada por timeout",
                ExecutionTime: stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error ejecutando herramienta WASM {ToolName}", tool.Name);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: ex.Message,
                ExecutionTime: stopwatch.Elapsed
            );
        }
    }
}

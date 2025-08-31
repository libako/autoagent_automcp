using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mcp.Bundles;

namespace Mcp.Runtime;

/// <summary>
/// Runtime para ejecutar herramientas en subprocesos (Node.js, Python)
/// </summary>
public class SubprocessRuntime : IToolRuntime
{
    private readonly ILogger<SubprocessRuntime> _logger;
    private readonly Dictionary<string, string> _runtimeCommands;

    public SubprocessRuntime(ILogger<SubprocessRuntime> logger)
    {
        _logger = logger;
        _runtimeCommands = new Dictionary<string, string>
        {
            [RuntimeTypes.Node] = "node",
            [RuntimeTypes.Python] = "python3"
        };
    }

    public string RuntimeName => "subprocess";

    public bool CanExecute(ToolSpec tool)
    {
        return _runtimeCommands.ContainsKey(tool.Runtime);
    }

    /// <summary>
    /// Ejecuta una herramienta con la ruta del bundle especificada
    /// </summary>
    public async Task<ToolInvokeResult> InvokeAsync(ToolSpec tool, JsonElement arguments, string bundlePath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (!_runtimeCommands.TryGetValue(tool.Runtime, out var command))
        {
            throw new InvalidOperationException($"Runtime no soportado: {tool.Runtime}");
        }

        // Construir la ruta completa de la herramienta usando la ruta del bundle
        var toolPath = Path.Combine(bundlePath, tool.Entry);
        
        if (!File.Exists(toolPath))
        {
            throw new FileNotFoundException($"No se encontró el archivo de herramienta: {toolPath}");
        }

        var psi = new ProcessStartInfo(command, toolPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Configurar límites de memoria si están especificados
        if (tool.Limits?.MemMB > 0)
        {
            // Nota: Los límites de memoria deben ser manejados por el sistema operativo
            // o por contenedores. Aquí solo registramos la intención.
            _logger.LogDebug("Límite de memoria configurado: {MemMB}MB", tool.Limits.MemMB);
        }

        using var process = new Process { StartInfo = psi };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Configurar timeout
        if (tool.Limits?.TimeoutMs > 0)
        {
            cts.CancelAfter(TimeSpan.FromMilliseconds(tool.Limits.TimeoutMs));
        }

        var inputJson = arguments.GetRawText();
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        _logger.LogDebug("Ejecutando herramienta: {Command} {ToolPath}", command, toolPath);
        
        if (!process.Start())
        {
            throw new InvalidOperationException("No se pudo iniciar el proceso");
        }

        // Configurar streams de salida
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Enviar entrada
        await process.StandardInput.WriteAsync(inputJson.ToCharArray(), 0, inputJson.Length);
        await process.StandardInput.FlushAsync(cts.Token);
        process.StandardInput.Close();

        // Esperar a que termine
        await process.WaitForExitAsync(cts.Token);

        stopwatch.Stop();

        if (process.ExitCode != 0)
        {
            var errorOutput = errorBuilder.ToString();
            _logger.LogError("Herramienta falló con código {ExitCode}: {Error}", process.ExitCode, errorOutput);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: $"Proceso falló con código {process.ExitCode}: {errorOutput}",
                ExecutionTime: stopwatch.Elapsed
            );
        }

        var output = outputBuilder.ToString().Trim();
        if (string.IsNullOrEmpty(output))
        {
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: false,
                ErrorMessage: null,
                ExecutionTime: stopwatch.Elapsed
            );
        }

        try
        {
            var outputJson = JsonDocument.Parse(output);
            return new ToolInvokeResult(
                outputJson.RootElement.Clone(),
                IsError: false,
                ErrorMessage: null,
                ExecutionTime: stopwatch.Elapsed
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parseando salida JSON de la herramienta");
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: $"Error parseando salida JSON: {ex.Message}",
                ExecutionTime: stopwatch.Elapsed
            );
        }
    }

    /// <summary>
    /// Ejecuta una herramienta (método original para compatibilidad)
    /// </summary>
    public async Task<ToolInvokeResult> InvokeAsync(ToolSpec tool, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (!_runtimeCommands.TryGetValue(tool.Runtime, out var command))
            {
                throw new InvalidOperationException($"Runtime no soportado: {tool.Runtime}");
            }

            var toolPath = Path.Combine(tool.Entry);
            if (!Path.IsPathRooted(toolPath))
            {
                // Asumir que la ruta es relativa al directorio del bundle
                toolPath = Path.GetFullPath(toolPath);
            }

            if (!File.Exists(toolPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo de herramienta: {toolPath}");
            }

            var psi = new ProcessStartInfo(command, toolPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Configurar límites de memoria si están especificados
            if (tool.Limits?.MemMB > 0)
            {
                // Nota: Los límites de memoria deben ser manejados por el sistema operativo
                // o por contenedores. Aquí solo registramos la intención.
                _logger.LogDebug("Límite de memoria configurado: {MemMB}MB", tool.Limits.MemMB);
            }

            using var process = new Process { StartInfo = psi };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Configurar timeout
            if (tool.Limits?.TimeoutMs > 0)
            {
                cts.CancelAfter(TimeSpan.FromMilliseconds(tool.Limits.TimeoutMs));
            }

            var inputJson = arguments.GetRawText();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            _logger.LogDebug("Ejecutando herramienta: {Command} {ToolPath}", command, toolPath);
            
            if (!process.Start())
            {
                throw new InvalidOperationException("No se pudo iniciar el proceso");
            }

            // Configurar streams de salida
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Enviar entrada
            await process.StandardInput.WriteAsync(inputJson.ToCharArray(), 0, inputJson.Length);
            await process.StandardInput.FlushAsync(cts.Token);
            process.StandardInput.Close();

            // Esperar a que termine
            await process.WaitForExitAsync(cts.Token);

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                var errorOutput = errorBuilder.ToString();
                _logger.LogError("Herramienta falló con código {ExitCode}: {Error}", process.ExitCode, errorOutput);
                
                return new ToolInvokeResult(
                    JsonDocument.Parse("{}").RootElement.Clone(),
                    IsError: true,
                    ErrorMessage: $"Proceso falló con código {process.ExitCode}: {errorOutput}",
                    ExecutionTime: stopwatch.Elapsed
                );
            }

            var output = outputBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(output))
            {
                return new ToolInvokeResult(
                    JsonDocument.Parse("{}").RootElement.Clone(),
                    ExecutionTime: stopwatch.Elapsed
                );
            }

            try
            {
                var result = JsonDocument.Parse(output).RootElement.Clone();
                _logger.LogDebug("Herramienta ejecutada exitosamente en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                
                return new ToolInvokeResult(result, ExecutionTime: stopwatch.Elapsed);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parseando salida JSON de la herramienta: {Output}", output);
                
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
            _logger.LogWarning("Ejecución de herramienta cancelada después de {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
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
            _logger.LogError(ex, "Error ejecutando herramienta {ToolName}", tool.Name);
            
            return new ToolInvokeResult(
                JsonDocument.Parse("{}").RootElement.Clone(),
                IsError: true,
                ErrorMessage: ex.Message,
                ExecutionTime: stopwatch.Elapsed
            );
        }
    }
}

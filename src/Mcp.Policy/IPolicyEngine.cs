using System.Text.Json;
using Mcp.Bundles;

namespace Mcp.Policy;

/// <summary>
/// Contexto de evaluación de políticas
/// </summary>
public record PolicyContext(
    string ClientId,
    string ToolNamespace,
    string ToolName,
    JsonElement Arguments,
    ToolSpec ToolSpec,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Resultado de evaluación de políticas
/// </summary>
public record PolicyResult(
    bool IsAllowed,
    string? Reason = null,
    JsonElement? ModifiedArguments = null
);

/// <summary>
/// Interfaz del motor de políticas
/// </summary>
public interface IPolicyEngine
{
    /// <summary>
    /// Evalúa si una operación está permitida
    /// </summary>
    Task<PolicyResult> EvaluateAsync(PolicyContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Carga políticas desde un archivo
    /// </summary>
    Task LoadPoliciesAsync(string policyPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica si el motor está configurado
    /// </summary>
    bool IsConfigured { get; }
}

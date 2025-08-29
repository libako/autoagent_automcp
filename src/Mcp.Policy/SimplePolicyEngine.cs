using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mcp.Bundles;

namespace Mcp.Policy;

/// <summary>
/// Configuración del motor de políticas simple
/// </summary>
public class SimplePolicyEngineOptions
{
    public string DefaultPolicyPath { get; set; } = "policies";
    public bool AllowByDefault { get; set; } = true;
}

/// <summary>
/// Regla de política simple
/// </summary>
public record PolicyRule(
    string Name,
    string? ToolNamespace = null,
    string? ToolName = null,
    string? ClientId = null,
    bool Allow = true,
    string? Reason = null,
    JsonElement? Condition = null
);

/// <summary>
/// Motor de políticas simple basado en reglas JSON
/// </summary>
public class SimplePolicyEngine : IPolicyEngine
{
    private readonly ILogger<SimplePolicyEngine> _logger;
    private readonly SimplePolicyEngineOptions _options;
    private readonly List<PolicyRule> _rules = new();

    public SimplePolicyEngine(ILogger<SimplePolicyEngine> logger, IOptions<SimplePolicyEngineOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool IsConfigured => _rules.Count > 0;

    public async Task<PolicyResult> EvaluateAsync(PolicyContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Evaluando política para {ClientId} -> {ToolNamespace}.{ToolName}", 
            context.ClientId, context.ToolNamespace, context.ToolName);

        // Buscar reglas que coincidan
        var matchingRules = _rules.Where(rule => MatchesRule(rule, context)).ToList();

        if (!matchingRules.Any())
        {
            var result = _options.AllowByDefault ? 
                new PolicyResult(true, "No hay reglas específicas, permitido por defecto") :
                new PolicyResult(false, "No hay reglas específicas, denegado por defecto");
            
            _logger.LogDebug("Resultado de política: {Result}", result.IsAllowed);
            return result;
        }

        // Aplicar reglas en orden (última regla gana)
        var lastRule = matchingRules.Last();
        
        // Evaluar condición si existe
        if (lastRule.Condition.HasValue && !EvaluateCondition(lastRule.Condition.Value, context))
        {
            var result = new PolicyResult(false, $"Condición no cumplida para regla: {lastRule.Name}");
            _logger.LogDebug("Resultado de política: {Result} - {Reason}", result.IsAllowed, result.Reason);
            return result;
        }

        var policyResult = new PolicyResult(lastRule.Allow, lastRule.Reason);
        _logger.LogDebug("Resultado de política: {Result} - {Reason}", policyResult.IsAllowed, policyResult.Reason);
        
        return policyResult;
    }

    public async Task LoadPoliciesAsync(string policyPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(policyPath))
            {
                _logger.LogWarning("Directorio de políticas no existe: {PolicyPath}", policyPath);
                return;
            }

            var policyFiles = Directory.GetFiles(policyPath, "*.json", SearchOption.AllDirectories);
            _rules.Clear();

            foreach (var policyFile in policyFiles)
            {
                await LoadPolicyFileAsync(policyFile, cancellationToken);
            }

            _logger.LogInformation("Cargadas {RuleCount} reglas de política desde {PolicyPath}", _rules.Count, policyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando políticas desde {PolicyPath}", policyPath);
        }
    }

    private async Task LoadPolicyFileAsync(string policyFile, CancellationToken cancellationToken)
    {
        try
        {
            var policyJson = await File.ReadAllTextAsync(policyFile, cancellationToken);
            var rules = JsonSerializer.Deserialize<PolicyRule[]>(policyJson);
            
            if (rules != null)
            {
                _rules.AddRange(rules);
                _logger.LogDebug("Cargadas {RuleCount} reglas desde {PolicyFile}", rules.Length, policyFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando archivo de política: {PolicyFile}", policyFile);
        }
    }

    private bool MatchesRule(PolicyRule rule, PolicyContext context)
    {
        // Coincidencia de namespace
        if (!string.IsNullOrEmpty(rule.ToolNamespace) && 
            !string.Equals(rule.ToolNamespace, context.ToolNamespace, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Coincidencia de nombre de herramienta
        if (!string.IsNullOrEmpty(rule.ToolName) && 
            !string.Equals(rule.ToolName, context.ToolName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Coincidencia de cliente
        if (!string.IsNullOrEmpty(rule.ClientId) && 
            !string.Equals(rule.ClientId, context.ClientId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool EvaluateCondition(JsonElement condition, PolicyContext context)
    {
        try
        {
            // Implementación simple de evaluación de condiciones
            // En una implementación real, esto podría usar una librería como JsonPath
            
            if (condition.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in condition.EnumerateObject())
                {
                    switch (property.Name.ToLowerInvariant())
                    {
                        case "has_argument":
                            var argName = property.Value.GetString();
                            if (!string.IsNullOrEmpty(argName) && !context.Arguments.TryGetProperty(argName, out _))
                            {
                                return false;
                            }
                            break;
                            
                        case "argument_equals":
                            if (property.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var arg in property.Value.EnumerateObject())
                                {
                                    if (context.Arguments.TryGetProperty(arg.Name, out var actualValue))
                                    {
                                        if (!actualValue.ValueKind.Equals(arg.Value.ValueKind) || actualValue.GetRawText() != arg.Value.GetRawText())
                                        {
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluando condición de política");
            return false;
        }
    }
}

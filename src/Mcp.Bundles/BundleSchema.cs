using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;

namespace Mcp.Bundles;

/// <summary>
/// Esquema de un bundle de herramientas MCP
/// </summary>
public record BundleManifest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("tools")] ToolSpec[] Tools,
    [property: JsonPropertyName("resources")] ResourceSpec[] Resources,
    [property: JsonPropertyName("policies")] string[]? Policies = null,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata = null
);

/// <summary>
/// Especificación de una herramienta en un bundle
/// </summary>
public record ToolSpec(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("runtime")] string Runtime,
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("inputSchema")] string? InputSchema = null,
    [property: JsonPropertyName("outputSchema")] string? OutputSchema = null,
    [property: JsonPropertyName("permissions")] ToolPermissions? Permissions = null,
    [property: JsonPropertyName("limits")] ToolLimits? Limits = null,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata = null
);

/// <summary>
/// Especificación de un recurso en un bundle
/// </summary>
public record ResourceSpec(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata = null
);

/// <summary>
/// Permisos de una herramienta
/// </summary>
public record ToolPermissions(
    [property: JsonPropertyName("net")] string[]? Net = null,
    [property: JsonPropertyName("fs")] string[]? Fs = null,
    [property: JsonPropertyName("env")] string[]? Env = null
);

/// <summary>
/// Límites de ejecución de una herramienta
/// </summary>
public record ToolLimits(
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 5000,
    [property: JsonPropertyName("memMB")] int MemMB = 128,
    [property: JsonPropertyName("cpuMs")] int CpuMs = 1000
);

/// <summary>
/// Tipos de runtime soportados
/// </summary>
public static class RuntimeTypes
{
    public const string Node = "node18";
    public const string Python = "python3";
    public const string Wasm = "wasm";
    public const string Docker = "docker";
}

/// <summary>
/// Validador de esquemas de bundle
/// </summary>
public static class BundleSchemaValidator
{
    private static readonly string BundleSchemaJson = """
    {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "type": "object",
        "required": ["name", "namespace", "version", "protocol", "tools"],
        "properties": {
            "name": { "type": "string", "minLength": 1 },
            "namespace": { "type": "string", "minLength": 1 },
            "version": { "type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$" },
            "protocol": { "type": "string", "enum": ["1.1"] },
            "tools": { 
                "type": "array", 
                "items": { "$ref": "#/definitions/tool" },
                "minItems": 1
            },
            "resources": { 
                "type": "array", 
                "items": { "$ref": "#/definitions/resource" }
            },
            "policies": { 
                "type": "array", 
                "items": { "type": "string" }
            },
            "metadata": { "type": "object" }
        },
        "definitions": {
            "tool": {
                "type": "object",
                "required": ["name", "runtime", "entry"],
                "properties": {
                    "name": { "type": "string", "minLength": 1 },
                    "runtime": { "type": "string", "enum": ["node18", "python3", "wasm", "docker"] },
                    "entry": { "type": "string", "minLength": 1 },
                    "inputSchema": { "type": "string" },
                    "outputSchema": { "type": "string" },
                    "permissions": { "$ref": "#/definitions/permissions" },
                    "limits": { "$ref": "#/definitions/limits" },
                    "metadata": { "type": "object" }
                }
            },
            "resource": {
                "type": "object",
                "required": ["name", "schema"],
                "properties": {
                    "name": { "type": "string", "minLength": 1 },
                    "schema": { "type": "string", "minLength": 1 },
                    "metadata": { "type": "object" }
                }
            },
            "permissions": {
                "type": "object",
                "properties": {
                    "net": { "type": "array", "items": { "type": "string" } },
                    "fs": { "type": "array", "items": { "type": "string" } },
                    "env": { "type": "array", "items": { "type": "string" } }
                }
            },
            "limits": {
                "type": "object",
                "properties": {
                    "timeoutMs": { "type": "integer", "minimum": 100, "maximum": 30000 },
                    "memMB": { "type": "integer", "minimum": 16, "maximum": 1024 },
                    "cpuMs": { "type": "integer", "minimum": 100, "maximum": 10000 }
                }
            }
        }
    }
    """;

    /// <summary>
    /// Valida un manifiesto de bundle
    /// </summary>
    public static ValidationResult ValidateBundle(JsonElement bundleJson)
    {
        try
        {
            var schema = JsonSchema.FromJsonAsync(BundleSchemaJson).Result;
            var validationResult = schema.Validate(bundleJson.GetRawText());
            return new ValidationResult(validationResult.Count == 0, validationResult.Select(v => v.ToString()).ToArray());
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, new[] { ex.Message });
        }
    }

    /// <summary>
    /// Valida un manifiesto de bundle desde JSON string
    /// </summary>
    public static ValidationResult ValidateBundle(string bundleJson)
    {
        try
        {
            var schema = JsonSchema.FromJsonAsync(BundleSchemaJson).Result;
            var validationResult = schema.Validate(bundleJson);
            return new ValidationResult(validationResult.Count == 0, validationResult.Select(v => v.ToString()).ToArray());
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, new[] { ex.Message });
        }
    }
}

/// <summary>
/// Resultado de validación
/// </summary>
public record ValidationResult(bool IsValid, string[] Errors);

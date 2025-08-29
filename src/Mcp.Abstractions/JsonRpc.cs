using System.Text.Json;

namespace Mcp.Abstractions;

/// <summary>
/// Contratos JSON-RPC 2.0 para el protocolo MCP
/// </summary>
public record JsonRpcRequest(string Jsonrpc, string Method, JsonElement? Params, string Id);

public record JsonRpcResponse(string Jsonrpc, JsonElement? Result, JsonRpcError? Error, string Id);

public record JsonRpcError(int Code, string Message, JsonElement? Data);

/// <summary>
/// Parámetros para el método initialize del protocolo MCP
/// </summary>
public record InitializeParams(
    string ClientName, 
    string ProtocolVersion, 
    string[] DesiredCaps,
    JsonElement? ClientInfo = null
);

/// <summary>
/// Capacidades del servidor MCP
/// </summary>
public record Capabilities(
    string[] Features, 
    string ProtocolVersion,
    JsonElement? ServerInfo = null
);

/// <summary>
/// Descriptor de una herramienta MCP
/// </summary>
public record ToolDescriptor(
    string Namespace, 
    string Name, 
    string Version,
    string? Description = null,
    JsonElement? InputSchema = null,
    JsonElement? OutputSchema = null
);

/// <summary>
/// Descriptor de un recurso MCP
/// </summary>
public record ResourceDescriptor(
    string Namespace,
    string Name,
    string Version,
    string? Description = null,
    JsonElement? Schema = null
);

/// <summary>
/// Parámetros para invocar una herramienta
/// </summary>
public record ToolInvokeParams(
    string Namespace,
    string Name,
    JsonElement Arguments
);

/// <summary>
/// Resultado de la invocación de una herramienta
/// </summary>
public record ToolInvokeResult(
    JsonElement Content,
    bool IsError = false
);

/// <summary>
/// Constantes del protocolo MCP
/// </summary>
public static class McpConstants
{
    public const string ProtocolVersion = "1.1";
    public const string JsonRpcVersion = "2.0";
    
    // Métodos del protocolo
    public const string Initialize = "initialize";
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
    public const string ResourcesList = "resources/list";
    public const string ResourcesRead = "resources/read";
    public const string NotificationsNotify = "notifications/notify";
    
    // Códigos de error
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ServerErrorStart = -32000;
    public const int ServerErrorEnd = -32099;
}

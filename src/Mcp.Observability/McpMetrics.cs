using Prometheus;

namespace Mcp.Observability;

/// <summary>
/// Métricas personalizadas para el servidor MCP
/// </summary>
public static class McpMetrics
{
    /// <summary>
    /// Contador de solicitudes totales
    /// </summary>
    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "mcp_requests_total",
        "Total number of MCP requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "status", "tool_namespace", "tool_name" }
        });

    /// <summary>
    /// Histograma de latencia de solicitudes
    /// </summary>
    public static readonly Histogram RequestDuration = Metrics.CreateHistogram(
        "mcp_request_duration_seconds",
        "Duration of MCP requests",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "tool_namespace", "tool_name" },
            Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
        });

    /// <summary>
    /// Contador de ejecuciones de herramientas
    /// </summary>
    public static readonly Counter ToolExecutionsTotal = Metrics.CreateCounter(
        "mcp_tool_executions_total",
        "Total number of tool executions",
        new CounterConfiguration
        {
            LabelNames = new[] { "tool_namespace", "tool_name", "runtime", "status" }
        });

    /// <summary>
    /// Histograma de duración de ejecución de herramientas
    /// </summary>
    public static readonly Histogram ToolExecutionDuration = Metrics.CreateHistogram(
        "mcp_tool_execution_duration_seconds",
        "Duration of tool executions",
        new HistogramConfiguration
        {
            LabelNames = new[] { "tool_namespace", "tool_name", "runtime" },
            Buckets = new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0 }
        });

    /// <summary>
    /// Contador de bundles cargados
    /// </summary>
    public static readonly Counter BundlesLoaded = Metrics.CreateCounter(
        "mcp_bundles_loaded_total",
        "Total number of bundles loaded",
        new CounterConfiguration
        {
            LabelNames = new[] { "bundle_namespace", "bundle_version" }
        });

    /// <summary>
    /// Gauge de bundles activos
    /// </summary>
    public static readonly Gauge ActiveBundles = Metrics.CreateGauge(
        "mcp_active_bundles",
        "Number of currently active bundles");

    /// <summary>
    /// Gauge de herramientas activas
    /// </summary>
    public static readonly Gauge ActiveTools = Metrics.CreateGauge(
        "mcp_active_tools",
        "Number of currently active tools",
        new GaugeConfiguration
        {
            LabelNames = new[] { "runtime" }
        });

    /// <summary>
    /// Contador de errores de políticas
    /// </summary>
    public static readonly Counter PolicyErrors = Metrics.CreateCounter(
        "mcp_policy_errors_total",
        "Total number of policy evaluation errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "tool_namespace", "tool_name", "error_type" }
        });

    /// <summary>
    /// Contador de conexiones WebSocket activas
    /// </summary>
    public static readonly Gauge ActiveWebSocketConnections = Metrics.CreateGauge(
        "mcp_websocket_connections_active",
        "Number of active WebSocket connections");

    /// <summary>
    /// Contador de conexiones WebSocket totales
    /// </summary>
    public static readonly Counter WebSocketConnectionsTotal = Metrics.CreateCounter(
        "mcp_websocket_connections_total",
        "Total number of WebSocket connections",
        new CounterConfiguration
        {
            LabelNames = new[] { "status" }
        });
}

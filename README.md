# MCP AutoServer

Un servidor MCP (Model Context Protocol) autodescubrible que actúa como caja de herramientas vacía que se puebla dinámicamente con bundles de capacidades, detecta su entorno y negocia contrato/seguridad con los clientes.

## Características

- **Autodescubrimiento**: Anuncio automático vía mDNS/zeroconf y endpoint `.well-known/mcp`
- **Hot-plug/hot-reload**: Agregar/actualizar/eliminar bundles de herramientas en caliente
- **Múltiples runtimes**: Soporte para Node.js, Python, WebAssembly (WASM) y Docker
- **Seguridad**: Motor de políticas configurable y autenticación JWT/mTLS
- **Observabilidad**: Métricas Prometheus y trazas OpenTelemetry
- **Rate limiting**: Control de velocidad de solicitudes
- **Compatibilidad**: Cumplimiento estricto del protocolo MCP 1.1

## Arquitectura

```
+----------------------- MCP Client(s) -----------------------+
|  IDE/Agent/LLM  |  SDK Client  |  HTTP/MQ Gateway (opt)    |
+------------------^------------^----------------------------+
                   |            |
              (MCP Stream)  (MCP over WS/HTTP2)
                   |            |
+------------------+------------+----------------------------+
|                   MCP Server Core                           |
|  - Session Manager        - Capability Negotiator           |
|  - Router (Tools/Res)     - JSON Schema Validator           |
|  - RateLimiter (per key)  - Policy Engine                   |
|  - AuthN/AuthZ            - Telemetry (OTel)                |
+------------------+------------+----------------------------+
                   |            |
          +--------+--+      +--+--------+
          | RuntimeMgr |      | Discovery |
          | (Sandbox)  |      |  Service  |
          +-----+------+      +-----+-----+
                |                   |
      +---------+----------+   +----+--------------------+
      | Subproc |  WASM   |   | mDNS | .well-known | Reg |
      |  OCI    |  VMs    |   | FS   | HTTP(S)      | API |
      +---------+---------+   +---------------------------+
                |
      +---------+----------------+ 
      |    Tool Bundles Store    |
      |  (manifests + adapters)  |
      +--------------------------+
```

## Requisitos

- .NET 8.0 o superior
- Node.js 18+ (para herramientas Node.js)
- Python 3.8+ (para herramientas Python)
- Wasmtime (para herramientas WASM)

## Instalación

1. Clonar el repositorio:
```bash
git clone <repository-url>
cd MCP_AutoServer
```

2. Restaurar dependencias:
```bash
dotnet restore
```

3. Compilar el proyecto:
```bash
dotnet build
```

4. Ejecutar el servidor:
```bash
dotnet run --project src/Mcp.Server
```

## Configuración

El servidor se configura mediante `appsettings.json`:

```json
{
  "BundleLoader": {
    "BundlesPath": "bundles",
    "EnableHotReload": true,
    "ReloadDelayMs": 1000
  },
  "Discovery": {
    "ServiceName": "MCP AutoServer",
    "Port": 8970,
    "ProtocolVersion": "1.1",
    "Capabilities": ["tools", "resources", "events"],
    "AuthMethods": ["oidc", "apikey", "mtls"],
    "EnableMdns": true
  },
  "PolicyEngine": {
    "DefaultPolicyPath": "policies",
    "AllowByDefault": true
  }
}
```

## Estructura de Bundles

Los bundles de herramientas siguen esta estructura:

```
bundles/
  my-tools/
    bundle.json              # Manifiesto del bundle
    schemas/                 # Esquemas JSON
      tool.in.json
      tool.out.json
    tools/                   # Implementaciones de herramientas
      tool.js               # Node.js
      tool.py               # Python
      tool.wasm             # WebAssembly
    policies/               # Políticas OPA (opcional)
      allowlist.rego
```

### Ejemplo de bundle.json

```json
{
  "name": "my-tools",
  "namespace": "my",
  "version": "1.0.0",
  "protocol": "1.1",
  "tools": [
    {
      "name": "example",
      "runtime": "node18",
      "entry": "tools/example.js",
      "inputSchema": "schemas/example.in.json",
      "outputSchema": "schemas/example.out.json",
      "permissions": {
        "net": ["https://api.example.com"],
        "fs": ["/data:ro"]
      },
      "limits": {
        "timeoutMs": 5000,
        "memMB": 128
      }
    }
  ],
  "resources": [],
  "policies": []
}
```

## Uso

### 1. Descubrimiento

El servidor se anuncia automáticamente vía mDNS con el servicio `_mcp._tcp.local`.

Para descubrir manualmente:

```bash
# Endpoint .well-known/mcp
curl http://localhost:8970/.well-known/mcp

# Endpoint de salud
curl http://localhost:8970/health

# Métricas Prometheus
curl http://localhost:8970/metrics
```

### 2. Conexión WebSocket

Conectar al endpoint WebSocket:

```javascript
const ws = new WebSocket('ws://localhost:8970/ws');

// Inicializar sesión
ws.send(JSON.stringify({
  jsonrpc: "2.0",
  method: "initialize",
  params: {
    clientName: "MyClient",
    protocolVersion: "1.1",
    desiredCaps: ["tools", "resources"]
  },
  id: "1"
}));

// Listar herramientas
ws.send(JSON.stringify({
  jsonrpc: "2.0",
  method: "tools/list",
  params: {},
  id: "2"
}));

// Ejecutar herramienta
ws.send(JSON.stringify({
  jsonrpc: "2.0",
  method: "tools/call",
  params: {
    namespace: "kb",
    name: "search",
    arguments: {
      query: "MCP protocol",
      limit: 5
    }
  },
  id: "3"
}));
```

### 3. Hot-reload de Bundles

Los bundles se recargan automáticamente cuando se modifican:

```bash
# Agregar nuevo bundle
cp -r my-new-tools bundles/

# Modificar bundle existente
# Los cambios se detectan automáticamente
```

## Runtimes Soportados

### Node.js

```javascript
#!/usr/bin/env node

let input = '';
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
    const args = JSON.parse(input);
    const result = processTool(args);
    console.log(JSON.stringify(result));
});

function processTool(args) {
    // Implementación de la herramienta
    return { result: "success" };
}
```

### Python

```python
#!/usr/bin/env python3
import json
import sys

def process_tool(args):
    # Implementación de la herramienta
    return {"result": "success"}

if __name__ == "__main__":
    input_data = sys.stdin.read()
    args = json.loads(input_data)
    result = process_tool(args)
    print(json.dumps(result))
```

### WebAssembly

```rust
use wasm_bindgen::prelude::*;
use serde::{Deserialize, Serialize};

#[derive(Deserialize)]
struct Args {
    input: String,
}

#[derive(Serialize)]
struct Result {
    output: String,
}

#[wasm_bindgen]
pub fn process_tool(input: &str) -> String {
    let args: Args = serde_json::from_str(input).unwrap();
    let result = Result {
        output: format!("Processed: {}", args.input),
    };
    serde_json::to_string(&result).unwrap()
}
```

## Políticas de Seguridad

Las políticas se definen en archivos JSON:

```json
[
  {
    "name": "allow_search",
    "toolNamespace": "kb",
    "toolName": "search",
    "allow": true,
    "reason": "Búsqueda permitida para todos los usuarios"
  },
  {
    "name": "deny_admin_tools",
    "toolNamespace": "admin",
    "allow": false,
    "reason": "Herramientas administrativas restringidas"
  },
  {
    "name": "conditional_access",
    "toolNamespace": "sensitive",
    "condition": {
      "has_argument": "authorization",
      "argument_equals": {
        "role": "admin"
      }
    },
    "allow": true
  }
]
```

## Métricas y Observabilidad

### Métricas Prometheus

- `mcp_requests_total`: Total de solicitudes MCP
- `mcp_request_duration_seconds`: Duración de solicitudes
- `mcp_tool_executions_total`: Total de ejecuciones de herramientas
- `mcp_tool_execution_duration_seconds`: Duración de ejecuciones
- `mcp_active_bundles`: Bundles activos
- `mcp_active_tools`: Herramientas activas por runtime
- `mcp_websocket_connections_active`: Conexiones WebSocket activas

### Trazas OpenTelemetry

El servidor genera trazas para:
- Solicitudes MCP
- Ejecución de herramientas
- Evaluación de políticas
- Operaciones de bundles

## Desarrollo

### Estructura del Proyecto

```
src/
  Mcp.Server/           # Host ASP.NET Core
  Mcp.Abstractions/     # Contratos JSON-RPC/MCP
  Mcp.Runtime/          # Runtimes de herramientas
  Mcp.Policy/           # Motor de políticas
  Mcp.Observability/    # Métricas y trazas
  Mcp.Bundles/          # Cargador de bundles

tools/
  Tools.Sample.TS/      # Ejemplo Node.js
  Tools.Sample.Wasm/    # Ejemplo WASM

bundles/                # Bundles de herramientas
  kb-tools/             # Bundle de ejemplo
```

### Compilar y Ejecutar

```bash
# Compilar
dotnet build

# Ejecutar en modo desarrollo
dotnet run --project src/Mcp.Server

# Ejecutar con configuración personalizada
dotnet run --project src/Mcp.Server --environment Production
```

### Tests

```bash
# Ejecutar tests unitarios
dotnet test

# Ejecutar tests de integración
dotnet test --filter Category=Integration
```

## Roadmap

### MVP (Completado)
- ✅ WebSocket + JSON-RPC básico
- ✅ Bundles por sistema de archivos
- ✅ Subproceso Node.js
- ✅ .well-known/mcp + Zeroconf

### R1 (En desarrollo)
- 🔄 Runtime Wasmtime + límites
- 🔄 Auth JWT/OIDC + Rate limiting
- 🔄 OpenTelemetry + métricas

### R2 (Planificado)
- 📋 Políticas OPA + firmas de bundles
- 📋 Ejecución vía Docker/K8s
- 📋 Registro de bundles (OCI/Git)
- 📋 Hot-reload robusto

## Contribuir

1. Fork el repositorio
2. Crear una rama para tu feature (`git checkout -b feature/amazing-feature`)
3. Commit tus cambios (`git commit -m 'Add amazing feature'`)
4. Push a la rama (`git push origin feature/amazing-feature`)
5. Abrir un Pull Request

## Licencia

Este proyecto está licenciado bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para detalles.

## Soporte

- **Documentación**: [Wiki del proyecto](https://github.com/your-org/mcp-autoserver/wiki)
- **Issues**: [GitHub Issues](https://github.com/your-org/mcp-autoserver/issues)
- **Discusiones**: [GitHub Discussions](https://github.com/your-org/mcp-autoserver/discussions)

## Agradecimientos

- [Model Context Protocol](https://modelcontextprotocol.io/) por la especificación
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet) por el framework web
- [Wasmtime](https://wasmtime.dev/) por el runtime WASM
- [OpenTelemetry](https://opentelemetry.io/) por la observabilidad

# 🚀 Instrucciones para MCP Server - Evitar Error 400

## ✅ **Estado Actual del Servidor**

El **MCP AutoServer** está funcionando correctamente en `http://localhost:8970` con las siguientes características:

- ✅ **Autodescubrimiento**: 6 ubicaciones configuradas
- ✅ **4 herramientas disponibles**: kb.search, kb.summarize, meme.generate, meme.templates
- ✅ **2 runtimes**: subprocess (Node.js/Python) y wasm (WebAssembly)
- ✅ **Endpoints HTTP REST**: Operativos para listar e invocar herramientas
- ✅ **Endpoint de descubrimiento**: `/.well-known/mcp` con lista completa de herramientas

## 🔧 **Cómo Evitar el Error 400**

### **1. Para WebSocket (JSON-RPC 2.0)**

El error 400 en WebSocket puede ocurrir por:

#### **A. Parámetros Incorrectos**
```json
// ❌ INCORRECTO - Falta parámetro requerido
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "meme.generate",
    "arguments": {
      "bottomText": "Solo texto inferior"
      // ❌ Falta "topText" que es requerido
    }
  }
}
```

```json
// ✅ CORRECTO - Todos los parámetros requeridos
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "meme.generate",
    "arguments": {
      "topText": "¡Hola Mundo!",        // ✅ Requerido
      "bottomText": "MCP Server funciona", // ✅ Opcional
      "template": "drake",              // ✅ Opcional
      "style": "classic"                // ✅ Opcional
    }
  }
}
```

#### **B. Formato de Nombre Incorrecto**
```json
// ❌ INCORRECTO - Formato incorrecto
{
  "method": "tools/call",
  "params": {
    "name": "meme_generate",  // ❌ Debe ser "meme.generate"
    "arguments": { ... }
  }
}
```

```json
// ✅ CORRECTO - Formato namespace.toolName
{
  "method": "tools/call",
  "params": {
    "name": "meme.generate",  // ✅ Formato correcto
    "arguments": { ... }
  }
}
```

#### **C. Secuencia de Mensajes Correcta**
```javascript
// 1. Inicializar conexión
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "1.1",
    "capabilities": {
      "tools": {}
    },
    "clientInfo": {
      "name": "Mi Cliente",
      "version": "1.0.0"
    }
  }
}

// 2. Listar herramientas
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}

// 3. Invocar herramienta
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "meme.generate",
    "arguments": {
      "topText": "¡Hola Mundo!",
      "bottomText": "MCP Server funciona",
      "template": "drake",
      "style": "classic"
    }
  }
}
```

### **2. Para HTTP REST**

#### **A. Listar Herramientas**
```bash
GET http://localhost:8970/tools
```

#### **B. Invocar Herramienta**
```bash
POST http://localhost:8970/tools/meme.generate:invoke
Content-Type: application/json

{
  "topText": "¡Hola Mundo!",
  "bottomText": "MCP Server funciona",
  "template": "drake",
  "style": "classic"
}
```

#### **C. Descubrimiento**
```bash
GET http://localhost:8970/.well-known/mcp
```

## 📋 **Esquemas de Herramientas Disponibles**

### **meme.generate**
```json
{
  "topText": "string (requerido, max 100 chars)",
  "bottomText": "string (opcional, max 100 chars)",
  "template": "string (opcional: drake, distracted-boyfriend, two-buttons, change-my-mind, surprised-pikachu, random)",
  "style": "string (opcional: classic, impact, comic, random, default: classic)"
}
```

### **meme.templates**
```json
{
  // No requiere parámetros
}
```

### **kb.search**
```json
{
  "query": "string (requerido)",
  "limit": "number (opcional, default: 10)"
}
```

### **kb.summarize**
```json
{
  "text": "string (requerido)",
  "maxLength": "number (opcional, default: 200)"
}
```

## 🔍 **Endpoints de Debug**

### **Verificar Estado del Servidor**
```bash
GET http://localhost:8970/health
```

### **Ver Runtimes Disponibles**
```bash
GET http://localhost:8970/debug/runtimes
```

### **Ver Detalles de Herramienta**
```bash
GET http://localhost:8970/debug/tools/meme.generate
```

## 🛠️ **Solución al Error 400**

### **Para el Equipo de Backend:**

1. **Verificar Parámetros**: Asegúrate de incluir todos los parámetros requeridos según el esquema
2. **Formato Correcto**: Usa `namespace.toolName` para el nombre de la herramienta
3. **Secuencia Correcta**: Sigue la secuencia initialize → tools/list → tools/call
4. **JSON Válido**: Verifica que el JSON esté bien formateado
5. **Headers Correctos**: Incluye `Content-Type: application/json` para HTTP REST

### **Ejemplo de Cliente WebSocket Funcional**
```javascript
const WebSocket = require('ws');

const ws = new WebSocket('ws://localhost:8970/ws');

ws.on('open', () => {
    // 1. Inicializar
    ws.send(JSON.stringify({
        jsonrpc: "2.0",
        id: 1,
        method: "initialize",
        params: {
            protocolVersion: "1.1",
            capabilities: { tools: {} },
            clientInfo: { name: "Test Client", version: "1.0.0" }
        }
    }));
});

ws.on('message', (data) => {
    const response = JSON.parse(data.toString());
    console.log('Respuesta:', response);
    
    if (response.result && response.result.protocolVersion) {
        // 2. Listar herramientas
        ws.send(JSON.stringify({
            jsonrpc: "2.0",
            id: 2,
            method: "tools/list",
            params: {}
        }));
    }
    
    if (response.result && response.result.tools) {
        // 3. Invocar herramienta
        ws.send(JSON.stringify({
            jsonrpc: "2.0",
            id: 3,
            method: "tools/call",
            params: {
                name: "meme.generate",
                arguments: {
                    topText: "¡Hola Mundo!",
                    bottomText: "MCP Server funciona",
                    template: "drake",
                    style: "classic"
                }
            }
        }));
    }
});
```

## 🎯 **Resumen**

- ✅ **El servidor funciona correctamente**
- ✅ **Los endpoints HTTP REST están operativos**
- ✅ **El autodescubrimiento está funcionando**
- ✅ **Las herramientas se pueden invocar**
- 🔧 **El error 400 se evita siguiendo los esquemas y formatos correctos**

¡El MCP Server está listo para usar! 🚀

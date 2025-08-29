const WebSocket = require('ws');

class MCPClient {
    constructor(url) {
        this.ws = new WebSocket(url);
        this.messageId = 1;
        this.setupEventHandlers();
    }

    setupEventHandlers() {
        this.ws.on('open', () => {
            console.log('✅ Conectado al MCP Server');
            console.log('🔄 Enviando mensaje de inicialización...');
            this.initialize();
        });

        this.ws.on('message', (data) => {
            const response = JSON.parse(data.toString());
            console.log('📨 Respuesta recibida:', JSON.stringify(response, null, 2));
            this.handleResponse(response);
        });

        this.ws.on('error', (error) => {
            console.error('❌ Error WebSocket:', error);
        });

        this.ws.on('close', (code, reason) => {
            console.log(`🔌 Conexión cerrada: ${code} - ${reason}`);
        });
    }

    sendMessage(method, params = {}) {
        const message = {
            jsonrpc: "2.0",
            id: this.messageId++,
            method: method,
            params: params
        };
        
        console.log('📤 Enviando mensaje:', JSON.stringify(message, null, 2));
        this.ws.send(JSON.stringify(message));
    }

    initialize() {
        this.sendMessage('initialize', {
            protocolVersion: "1.1",
            capabilities: {
                tools: {}
            },
            clientInfo: {
                name: "Test Client",
                version: "1.0.0"
            }
        });
    }

    listTools() {
        console.log('🔄 Enviando lista de herramientas...');
        this.sendMessage('tools/list', {});
    }

    callTool(namespace, name, args) {
        console.log(`🔄 Invocando herramienta: ${namespace}.${name}`);
        this.sendMessage('tools/call', {
            name: `${namespace}.${name}`,
            arguments: args
        });
    }

    handleResponse(response) {
        if (response.error) {
            console.error('❌ Error en respuesta:', response.error);
            return;
        }

        // Si es una respuesta de initialize, continuar con tools/list
        if (response.result && response.result.protocolVersion) {
            console.log('✅ Inicialización exitosa');
            setTimeout(() => {
                this.listTools();
            }, 500);
        }

        // Si es una respuesta de tools/list, probar una herramienta
        if (response.result && response.result.tools) {
            console.log('✅ Lista de herramientas recibida');
            if (response.result.tools.length > 0) {
                const firstTool = response.result.tools[0];
                console.log('🔧 Probando herramienta:', firstTool.name);
                
                // Enviar parámetros correctos según el esquema
                const toolArgs = {
                    topText: "¡Hola Mundo!",
                    bottomText: "MCP Server funciona",
                    template: "drake",
                    style: "classic"
                };
                
                setTimeout(() => {
                    this.callTool("meme", "generate", toolArgs);
                }, 500);
            }
        }

        // Si es una respuesta de tools/call
        if (response.result && response.result.content) {
            console.log('✅ Herramienta ejecutada exitosamente');
            console.log('📄 Contenido:', response.result.content);
        }
    }
}

// Función para probar diferentes mensajes
function testInvalidMessages() {
    console.log('\n🧪 Probando mensajes inválidos...');
    
    const ws = new WebSocket('ws://localhost:8970/ws');
    
    ws.on('open', () => {
        console.log('✅ Conectado para pruebas de mensajes inválidos');
        
        // Mensaje sin jsonrpc
        setTimeout(() => {
            console.log('\n📤 Enviando mensaje sin jsonrpc...');
            ws.send(JSON.stringify({
                id: 1,
                method: "initialize",
                params: {}
            }));
        }, 1000);

        // Mensaje con método inexistente
        setTimeout(() => {
            console.log('\n📤 Enviando método inexistente...');
            ws.send(JSON.stringify({
                jsonrpc: "2.0",
                id: 2,
                method: "invalid/method",
                params: {}
            }));
        }, 2000);

        // Mensaje con JSON malformado
        setTimeout(() => {
            console.log('\n📤 Enviando JSON malformado...');
            ws.send('{"jsonrpc": "2.0", "id": 3, "method": "initialize", "params": {');
        }, 3000);

        // Cerrar después de las pruebas
        setTimeout(() => {
            ws.close();
        }, 4000);
    });

    ws.on('message', (data) => {
        const response = JSON.parse(data.toString());
        console.log('📨 Respuesta:', JSON.stringify(response, null, 2));
    });

    ws.on('error', (error) => {
        console.error('❌ Error:', error);
    });
}

// Ejecutar pruebas
console.log('🚀 Iniciando pruebas del MCP Server...');
console.log('📍 URL: ws://localhost:8970/ws');

// Prueba principal
const client = new MCPClient('ws://localhost:8970/ws');

// Pruebas de mensajes inválidos después de 5 segundos
setTimeout(() => {
    testInvalidMessages();
}, 5000);

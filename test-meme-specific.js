const WebSocket = require('ws');

class MCPMemeTester {
    constructor(url) {
        this.ws = new WebSocket(url);
        this.messageId = 1;
        this.setupEventHandlers();
    }

    setupEventHandlers() {
        this.ws.on('open', () => {
            console.log('✅ Conectado al MCP Server');
            console.log('🔄 Inicializando sesión...');
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
            protocolVersion: "2024-11-05",
            capabilities: {
                tools: {}
            },
            clientInfo: {
                name: "Meme Tester",
                version: "1.0.0"
            }
        });
    }

    listTools() {
        console.log('🔄 Listando herramientas disponibles...');
        this.sendMessage('tools/list', {});
    }

    callMemeGenerate() {
        console.log('🔄 Invocando meme.generate...');
        this.sendMessage('tools/call', {
            name: "meme.generate",
            arguments: {
                topText: "Test de meme después de la corrección",
                bottomText: "¡Funciona!",
                template: "drake",
                style: "classic"
            }
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

        // Si es una respuesta de tools/list, probar meme.generate
        if (response.result && response.result.tools) {
            console.log('✅ Lista de herramientas recibida');
            console.log(`📋 Herramientas disponibles: ${response.result.tools.length}`);
            
            // Buscar la herramienta meme.generate
            const memeTool = response.result.tools.find(tool => tool.name === 'meme.generate');
            if (memeTool) {
                console.log('🎭 Herramienta meme.generate encontrada');
                setTimeout(() => {
                    this.callMemeGenerate();
                }, 500);
            } else {
                console.log('⚠️  Herramienta meme.generate no encontrada');
                console.log('Herramientas disponibles:');
                response.result.tools.forEach(tool => {
                    console.log(`  - ${tool.name}: ${tool.description || 'Sin descripción'}`);
                });
            }
        }

        // Si es una respuesta de tools/call
        if (response.result && response.result.content) {
            console.log('🎉 Herramienta ejecutada exitosamente!');
            console.log('📄 Resultado:', JSON.stringify(response.result.content, null, 2));
            
            // Cerrar conexión después de la prueba
            setTimeout(() => {
                this.ws.close();
            }, 1000);
        }
    }
}

console.log('🎭 Probando herramienta meme.generate del MCP Server...');
console.log('📍 Conectando a: ws://localhost:8970/ws');

const tester = new MCPMemeTester('ws://localhost:8970/ws');

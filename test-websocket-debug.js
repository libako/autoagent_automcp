const WebSocket = require('ws');

class MCPClientDebug {
    constructor(url) {
        this.url = url;
        this.messageId = 1;
        this.connected = false;
        this.setupConnection();
    }

    setupConnection() {
        console.log(`🔗 Intentando conectar a: ${this.url}`);
        
        this.ws = new WebSocket(this.url);
        
        this.ws.on('open', () => {
            console.log('✅ Conectado al MCP Server');
            this.connected = true;
            console.log('🔄 Enviando mensaje de inicialización...');
            this.initialize();
        });

        this.ws.on('message', (data) => {
            try {
                const response = JSON.parse(data.toString());
                console.log('📨 Respuesta recibida:', JSON.stringify(response, null, 2));
                this.handleResponse(response);
            } catch (error) {
                console.error('❌ Error parseando respuesta:', error);
                console.error('📄 Respuesta raw:', data.toString());
            }
        });

        this.ws.on('error', (error) => {
            console.error('❌ Error WebSocket:', error.message);
            console.error('🔍 Detalles del error:', error);
        });

        this.ws.on('close', (code, reason) => {
            console.log(`🔌 Conexión cerrada: ${code} - ${reason}`);
            this.connected = false;
        });

        // Timeout para cerrar si no hay respuesta
        setTimeout(() => {
            if (!this.connected) {
                console.log('⏰ Timeout - cerrando conexión');
                this.ws.close();
            }
        }, 10000);
    }

    sendMessage(method, params = {}) {
        if (!this.connected) {
            console.error('❌ No conectado al servidor');
            return;
        }

        const message = {
            jsonrpc: "2.0",
            id: this.messageId++,
            method: method,
            params: params
        };
        
        console.log('📤 Enviando mensaje:', JSON.stringify(message, null, 2));
        
        try {
            this.ws.send(JSON.stringify(message));
        } catch (error) {
            console.error('❌ Error enviando mensaje:', error);
        }
    }

    initialize() {
        this.sendMessage('initialize', {
            protocolVersion: "1.1",
            capabilities: {
                tools: {}
            },
            clientInfo: {
                name: "Test Client Debug",
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
            console.log(`📋 Total de herramientas: ${response.result.tools.length}`);
            
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
            } else {
                console.log('⚠️ No se encontraron herramientas disponibles');
            }
        }

        // Si es una respuesta de tools/call
        if (response.result && response.result.content) {
            console.log('✅ Herramienta ejecutada exitosamente');
            console.log('📄 Contenido:', response.result.content);
        }
    }

    close() {
        if (this.connected) {
            this.ws.close();
        }
    }
}

// Función para probar conexión HTTP básica
async function testHttpConnection() {
    console.log('\n🌐 Probando conexión HTTP básica...');
    
    try {
        const http = require('http');
        
        const options = {
            hostname: 'localhost',
            port: 8970,
            path: '/test',
            method: 'GET'
        };

        const req = http.request(options, (res) => {
            console.log(`📡 Status HTTP: ${res.statusCode}`);
            console.log(`📋 Headers:`, res.headers);
            
            let data = '';
            res.on('data', (chunk) => {
                data += chunk;
            });
            
            res.on('end', () => {
                console.log('📄 Respuesta HTTP:', data);
            });
        });

        req.on('error', (error) => {
            console.error('❌ Error HTTP:', error.message);
        });

        req.end();
    } catch (error) {
        console.error('❌ Error en prueba HTTP:', error);
    }
}

// Función para probar endpoint de herramientas
async function testToolsEndpoint() {
    console.log('\n🔧 Probando endpoint de herramientas...');
    
    try {
        const http = require('http');
        
        const options = {
            hostname: 'localhost',
            port: 8970,
            path: '/tools',
            method: 'GET'
        };

        const req = http.request(options, (res) => {
            console.log(`📡 Status HTTP: ${res.statusCode}`);
            
            let data = '';
            res.on('data', (chunk) => {
                data += chunk;
            });
            
            res.on('end', () => {
                try {
                    const tools = JSON.parse(data);
                    console.log(`📋 Herramientas disponibles: ${tools.length}`);
                    tools.forEach(tool => {
                        console.log(`  - ${tool.name} (${tool.runtime})`);
                    });
                } catch (error) {
                    console.error('❌ Error parseando herramientas:', error);
                    console.log('📄 Respuesta raw:', data);
                }
            });
        });

        req.on('error', (error) => {
            console.error('❌ Error HTTP:', error.message);
        });

        req.end();
    } catch (error) {
        console.error('❌ Error en prueba de herramientas:', error);
    }
}

// Ejecutar pruebas
console.log('🚀 Iniciando pruebas de debug del MCP Server...');
console.log('📍 URL: ws://localhost:8970/ws');

// Pruebas HTTP primero
testHttpConnection();
setTimeout(() => {
    testToolsEndpoint();
}, 1000);

// Prueba WebSocket después
setTimeout(() => {
    const client = new MCPClientDebug('ws://localhost:8970/ws');
    
    // Cerrar después de 15 segundos
    setTimeout(() => {
        client.close();
        process.exit(0);
    }, 15000);
}, 2000);



const WebSocket = require('ws');

console.log('🚀 Probando conexión WebSocket simple...');

// Crear conexión WebSocket básica
const ws = new WebSocket('ws://localhost:8970/ws');

ws.on('open', () => {
    console.log('✅ Conexión WebSocket establecida exitosamente');
    
    // Enviar un mensaje simple
    const message = {
        jsonrpc: "2.0",
        id: 1,
        method: "initialize",
        params: {
            protocolVersion: "1.1",
            capabilities: {
                tools: {}
            },
            clientInfo: {
                name: "Test Client",
                version: "1.0.0"
            }
        }
    };
    
    console.log('📤 Enviando mensaje:', JSON.stringify(message, null, 2));
    ws.send(JSON.stringify(message));
});

ws.on('message', (data) => {
    console.log('📨 Respuesta recibida:', data.toString());
    
    try {
        const response = JSON.parse(data.toString());
        console.log('✅ Mensaje procesado correctamente');
        console.log('📄 Respuesta JSON:', JSON.stringify(response, null, 2));
        
        // Si es la respuesta de initialize, enviar tools/list
        if (response.id === 1 && response.result) {
            console.log('🔄 Enviando tools/list...');
            const toolsListMessage = {
                jsonrpc: "2.0",
                id: 2,
                method: "tools/list",
                params: {}
            };
            ws.send(JSON.stringify(toolsListMessage));
        }
        
        // Si es la respuesta de tools/list, enviar resources/list
        else if (response.id === 2 && response.result) {
            console.log('🔄 Enviando resources/list...');
            const resourcesListMessage = {
                jsonrpc: "2.0",
                id: 3,
                method: "resources/list",
                params: {}
            };
            ws.send(JSON.stringify(resourcesListMessage));
        }
        
        // Si es la respuesta de resources/list, cerrar conexión
        else if (response.id === 3 && response.result) {
            console.log('✅ Todas las pruebas completadas, cerrando conexión...');
            setTimeout(() => {
                ws.close();
            }, 1000);
        }
        
    } catch (error) {
        console.error('❌ Error parseando respuesta:', error);
    }
});

ws.on('error', (error) => {
    console.error('❌ Error WebSocket:', error.message);
    console.error('🔍 Detalles del error:', error);
});

ws.on('close', (code, reason) => {
    console.log(`🔌 Conexión cerrada: ${code} - ${reason}`);
});

// Timeout para cerrar si no hay respuesta
setTimeout(() => {
    if (ws.readyState === WebSocket.OPEN) {
        console.log('⏰ Timeout - cerrando conexión');
        ws.close();
    }
}, 5000);


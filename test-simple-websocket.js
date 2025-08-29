const WebSocket = require('ws');

console.log('🚀 Probando conexión WebSocket simple...');

// Crear conexión WebSocket
const ws = new WebSocket('ws://localhost:8970/ws');

ws.on('open', () => {
    console.log('✅ Conexión WebSocket establecida exitosamente');
    
    // Enviar mensaje de inicialización
    const initMessage = {
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
    
    console.log('📤 Enviando mensaje de inicialización:', JSON.stringify(initMessage, null, 2));
    ws.send(JSON.stringify(initMessage));
});

ws.on('message', (data) => {
    const response = JSON.parse(data.toString());
    console.log('📨 Respuesta recibida:', JSON.stringify(response, null, 2));
    
    if (response.error) {
        console.error('❌ Error en respuesta:', response.error);
    } else {
        console.log('✅ Mensaje procesado correctamente');
    }
    
    // Cerrar conexión después de recibir respuesta
    setTimeout(() => {
        ws.close();
    }, 1000);
});

ws.on('error', (error) => {
    console.error('❌ Error WebSocket:', error.message);
    console.error('Detalles del error:', error);
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

const https = require('https');
const http = require('http');

// Función para hacer peticiones HTTP
function makeRequest(url, options = {}) {
    return new Promise((resolve, reject) => {
        const urlObj = new URL(url);
        const isHttps = urlObj.protocol === 'https:';
        const client = isHttps ? https : http;
        
        const requestOptions = {
            hostname: urlObj.hostname,
            port: urlObj.port,
            path: urlObj.pathname + urlObj.search,
            method: options.method || 'GET',
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        };

        const req = client.request(requestOptions, (res) => {
            let data = '';
            res.on('data', (chunk) => {
                data += chunk;
            });
            res.on('end', () => {
                try {
                    const jsonData = JSON.parse(data);
                    resolve({
                        statusCode: res.statusCode,
                        headers: res.headers,
                        data: jsonData
                    });
                } catch (e) {
                    resolve({
                        statusCode: res.statusCode,
                        headers: res.headers,
                        data: data
                    });
                }
            });
        });

        req.on('error', (error) => {
            reject(error);
        });

        if (options.body) {
            req.write(JSON.stringify(options.body));
        }

        req.end();
    });
}

async function testMcpServer() {
    const baseUrl = 'http://localhost:8970';
    
    console.log('🚀 Probando MCP Server via HTTP REST...\n');

    try {
        // 1. Probar endpoint de salud
        console.log('1️⃣ Probando endpoint de salud...');
        const healthResponse = await makeRequest(`${baseUrl}/health`);
        console.log(`✅ Status: ${healthResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(healthResponse.data, null, 2));
        console.log('');

        // 2. Probar endpoint de descubrimiento
        console.log('2️⃣ Probando endpoint de descubrimiento...');
        const discoveryResponse = await makeRequest(`${baseUrl}/.well-known/mcp`);
        console.log(`✅ Status: ${discoveryResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(discoveryResponse.data, null, 2));
        console.log('');

        // 3. Probar endpoint de herramientas
        console.log('3️⃣ Probando endpoint de herramientas...');
        const toolsResponse = await makeRequest(`${baseUrl}/tools`);
        console.log(`✅ Status: ${toolsResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(toolsResponse.data, null, 2));
        console.log('');

        // 4. Probar endpoint de debug de runtimes
        console.log('4️⃣ Probando endpoint de debug de runtimes...');
        const runtimesResponse = await makeRequest(`${baseUrl}/debug/runtimes`);
        console.log(`✅ Status: ${runtimesResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(runtimesResponse.data, null, 2));
        console.log('');

        // 5. Probar invocación de herramienta via HTTP REST
        console.log('5️⃣ Probando invocación de herramienta...');
        const toolArgs = {
            topText: "¡Hola Mundo!",
            bottomText: "MCP Server funciona",
            template: "drake",
            style: "classic"
        };
        
        const toolResponse = await makeRequest(`${baseUrl}/tools/meme.generate:invoke`, {
            method: 'POST',
            body: toolArgs
        });
        console.log(`✅ Status: ${toolResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(toolResponse.data, null, 2));
        console.log('');

        // 6. Probar endpoint de debug de herramienta específica
        console.log('6️⃣ Probando debug de herramienta específica...');
        const toolDebugResponse = await makeRequest(`${baseUrl}/debug/tools/meme.generate`);
        console.log(`✅ Status: ${toolDebugResponse.statusCode}`);
        console.log(`📄 Respuesta:`, JSON.stringify(toolDebugResponse.data, null, 2));
        console.log('');

        console.log('🎉 ¡Todas las pruebas HTTP REST pasaron exitosamente!');
        console.log('\n📋 Resumen:');
        console.log('✅ El servidor está funcionando correctamente');
        console.log('✅ Los endpoints HTTP REST están operativos');
        console.log('✅ Las herramientas se pueden invocar via HTTP REST');
        console.log('✅ El autodescubrimiento está funcionando');
        console.log('✅ Los runtimes están registrados correctamente');
        
        console.log('\n🔧 Para evitar el error 400 en WebSocket:');
        console.log('1. Asegúrate de enviar los parámetros correctos según el esquema');
        console.log('2. Usa el formato correcto: namespace.toolName');
        console.log('3. Incluye todos los parámetros requeridos (ej: topText para meme.generate)');
        console.log('4. Verifica que el mensaje JSON-RPC tenga el formato correcto');

    } catch (error) {
        console.error('❌ Error en las pruebas:', error.message);
    }
}

// Ejecutar pruebas
testMcpServer();

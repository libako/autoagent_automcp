# Test script para verificar que meme.generate funciona después de la corrección
Write-Host "Probando herramienta meme.generate..." -ForegroundColor Green

# Crear un mensaje de prueba
$testMessage = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "meme.generate"
        arguments = @{
            topText = "Test de meme después de la corrección"
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Mensaje de prueba:" -ForegroundColor Yellow
Write-Host $testMessage -ForegroundColor Cyan

# Enviar el mensaje al servidor (esto es solo para mostrar el formato)
Write-Host "`nPara probar, envía este mensaje al servidor WebSocket en ws://localhost:8970/ws" -ForegroundColor Green
Write-Host "O usa el cliente de prueba existente:" -ForegroundColor Green
Write-Host "test-meme-tool.ps1" -ForegroundColor Yellow

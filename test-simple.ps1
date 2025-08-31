# Script simple para probar que el servidor MCP esté funcionando
Write-Host "🧪 Probando servidor MCP..." -ForegroundColor Cyan

# Verificar que el servidor esté funcionando
Write-Host "`n1. Verificando estado del servidor..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:8970/health" -Method GET
    Write-Host "✅ Servidor funcionando: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: El servidor no está funcionando en http://localhost:8970" -ForegroundColor Red
    Write-Host "Ejecuta: dotnet run --project src/Mcp.Server" -ForegroundColor Yellow
    exit 1
}

# Obtener descriptor del servidor
Write-Host "`n2. Obteniendo descriptor del servidor..." -ForegroundColor Yellow
try {
    $descriptor = Invoke-RestMethod -Uri "http://localhost:8970/.well-known/mcp" -Method GET
    Write-Host "✅ Servidor MCP: $($descriptor.metadata.name)" -ForegroundColor Green
    Write-Host "   Versión: $($descriptor.version)" -ForegroundColor Gray
    Write-Host "   Capacidades: $($descriptor.capabilities -join ', ')" -ForegroundColor Gray
} catch {
    Write-Host "❌ Error obteniendo descriptor del servidor" -ForegroundColor Red
    exit 1
}

# Listar herramientas disponibles
Write-Host "`n3. Listando herramientas disponibles..." -ForegroundColor Yellow
try {
    $tools = Invoke-RestMethod -Uri "http://localhost:8970/tools/list" -Method GET
    if ($tools.tools) {
        Write-Host "✅ Herramientas encontradas:" -ForegroundColor Green
        foreach ($tool in $tools.tools) {
            Write-Host "   - $($tool.name): $($tool.description)" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠️  No se encontraron herramientas" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Error listando herramientas" -ForegroundColor Red
}

Write-Host "`n🎉 Prueba completada!" -ForegroundColor Green

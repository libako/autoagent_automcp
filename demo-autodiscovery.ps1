# Script de demostración del autodescubrimiento del MCP AutoServer
Write-Host "🔍 DEMOSTRACIÓN DEL AUTODESCUBRIMIENTO MCP" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Mostrar la estructura de directorios de autodescubrimiento
Write-Host "`n📁 ESTRUCTURA DE AUTODESCUBRIMIENTO:" -ForegroundColor Yellow
Write-Host "El servidor busca herramientas en múltiples ubicaciones:" -ForegroundColor White

$discoveryPaths = @(
    "./bundles",
    "../../bundles", 
    "$env:USERPROFILE/.mcp/bundles",
    "$env:PROGRAMDATA/MCP/bundles",
    "/usr/local/share/mcp/bundles",
    "/opt/mcp/bundles"
)

foreach ($path in $discoveryPaths) {
    $expandedPath = [System.Environment]::ExpandEnvironmentVariables($path)
    $exists = Test-Path $expandedPath
    $status = if ($exists) { "✅" } else { "❌" }
    Write-Host "  $status $expandedPath" -ForegroundColor $(if ($exists) { "Green" } else { "Red" })
}

# 2. Mostrar los bundles disponibles
Write-Host "`n🎁 BUNDLES DISPONIBLES:" -ForegroundColor Yellow
$bundlesDir = "bundles"
if (Test-Path $bundlesDir) {
    $bundles = Get-ChildItem -Path $bundlesDir -Directory
    foreach ($bundle in $bundles) {
        Write-Host "  📦 $($bundle.Name)" -ForegroundColor Green
        $manifestPath = Join-Path $bundle.FullName "bundle.json"
        if (Test-Path $manifestPath) {
            try {
                $manifest = Get-Content $manifestPath | ConvertFrom-Json
                Write-Host "     Namespace: $($manifest.namespace)" -ForegroundColor Gray
                Write-Host "     Version: $($manifest.version)" -ForegroundColor Gray
                Write-Host "     Tools: $($manifest.tools.Count)" -ForegroundColor Gray
                Write-Host "     Description: $($manifest.metadata.description)" -ForegroundColor Gray
            } catch {
                Write-Host "     ❌ Error leyendo manifest" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host "  ❌ No se encontró el directorio bundles/" -ForegroundColor Red
}

# 3. Crear directorios de autodescubrimiento adicionales
Write-Host "`n🔧 CONFIGURANDO AUTODESCUBRIMIENTO ADICIONAL:" -ForegroundColor Yellow

# Crear directorio de usuario
$userMcpDir = "$env:USERPROFILE/.mcp/bundles"
if (!(Test-Path $userMcpDir)) {
    New-Item -ItemType Directory -Path $userMcpDir -Force | Out-Null
    Write-Host "  ✅ Creado: $userMcpDir" -ForegroundColor Green
} else {
    Write-Host "  ✅ Existe: $userMcpDir" -ForegroundColor Green
}

# Crear directorio de programa
$programMcpDir = "$env:PROGRAMDATA/MCP/bundles"
if (!(Test-Path $programMcpDir)) {
    New-Item -ItemType Directory -Path $programMcpDir -Force | Out-Null
    Write-Host "  ✅ Creado: $programMcpDir" -ForegroundColor Green
} else {
    Write-Host "  ✅ Existe: $programMcpDir" -ForegroundColor Green
}

# 4. Copiar un bundle de ejemplo al directorio de usuario
Write-Host "`n📋 COPIANDO BUNDLE DE EJEMPLO:" -ForegroundColor Yellow
$sourceBundle = "bundles/meme-generator"
$destBundle = "$userMcpDir/meme-generator-user"

if (Test-Path $sourceBundle) {
    if (Test-Path $destBundle) {
        Remove-Item $destBundle -Recurse -Force
    }
    Copy-Item $sourceBundle $destBundle -Recurse
    Write-Host "  ✅ Copiado: $sourceBundle → $destBundle" -ForegroundColor Green
    
    # Modificar el manifest para mostrar que es una copia
    $manifestPath = Join-Path $destBundle "bundle.json"
    if (Test-Path $manifestPath) {
        $manifest = Get-Content $manifestPath | ConvertFrom-Json
        $manifest.metadata.description = "Copia de usuario del generador de memes"
        $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath
        Write-Host "  ✅ Manifest actualizado" -ForegroundColor Green
    }
} else {
    Write-Host "  ❌ No se encontró el bundle de origen" -ForegroundColor Red
}

# 5. Mostrar la configuración del servidor
Write-Host "`n⚙️ CONFIGURACIÓN DEL SERVIDOR:" -ForegroundColor Yellow
$configPath = "src/Mcp.Server/appsettings.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    Write-Host "  BundleLoader.BundlesPath: $($config.BundleLoader.BundlesPath)" -ForegroundColor White
    Write-Host "  BundleLoader.EnableHotReload: $($config.BundleLoader.EnableHotReload)" -ForegroundColor White
    Write-Host "  Discovery.AutoDiscoveryPaths: $($config.Discovery.AutoDiscoveryPaths.Count) rutas" -ForegroundColor White
} else {
    Write-Host "  ❌ No se encontró la configuración" -ForegroundColor Red
}

# 6. Instrucciones para probar
Write-Host "`n🚀 INSTRUCCIONES PARA PROBAR:" -ForegroundColor Yellow
Write-Host "1. Ejecuta: dotnet run --project src/Mcp.Server" -ForegroundColor White
Write-Host "2. Consulta: http://localhost:8970/.well-known/mcp" -ForegroundColor White
Write-Host "3. Verifica que aparezcan las herramientas de ambos directorios" -ForegroundColor White
Write-Host "4. Agrega nuevos bundles en cualquier directorio de autodescubrimiento" -ForegroundColor White

Write-Host "`n🎯 VENTAJAS DEL AUTODESCUBRIMIENTO:" -ForegroundColor Cyan
Write-Host "• No necesitas reiniciar el servidor al agregar herramientas" -ForegroundColor White
Write-Host "• Las herramientas pueden estar en cualquier ubicación del sistema" -ForegroundColor White
Write-Host "• Hot-reload detecta cambios automáticamente" -ForegroundColor White
Write-Host "• Múltiples usuarios pueden tener sus propias herramientas" -ForegroundColor White
Write-Host "• Instalación centralizada vs. personalizada" -ForegroundColor White

Write-Host "`n✨ ¡El autodescubrimiento real permite que las herramientas estén en cualquier lugar!" -ForegroundColor Green

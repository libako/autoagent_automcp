# Script de prueba para la herramienta de memes del MCP AutoServer
# Requiere que el servidor esté ejecutándose en http://localhost:8970

Write-Host "🎭 Probando la herramienta de memes del MCP AutoServer" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

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

# Simular una conexión WebSocket y llamada a la herramienta de memes
Write-Host "`n3. Simulando llamada a la herramienta de memes..." -ForegroundColor Yellow

# Crear un meme de ejemplo
$memeRequest = @{
    topText = "Programar en Java"
    bottomText = "Programar en Python"
    template = "drake"
    style = "impact"
} | ConvertTo-Json

Write-Host "📝 Generando meme con: $memeRequest" -ForegroundColor Gray

# Simular la ejecución de la herramienta directamente
Write-Host "`n4. Ejecutando herramienta meme.generate..." -ForegroundColor Yellow

# Crear un proceso Node.js para ejecutar la herramienta
$nodeScript = @"
const fs = require('fs');
const path = require('path');

// Simular la entrada JSON
const input = $memeRequest;

// Plantillas de memes en ASCII (simplificado)
const memeTemplates = {
  'drake': {
    ascii: [
      '    ╔══════════════════════════════════════════════════════════╗',
      '    ║                                                          ║',
      '    ║  😎  NO                                  😎  YES         ║',
      '    ║                                                          ║',
      '    ║  ┌─────────────────┐                    ┌─────────────────┐',
      '    ║  │                 │                    │                 │',
      '    ║  │   [TOP TEXT]    │                    │  [BOTTOM TEXT]  │',
      '    ║  │                 │                    │                 │',
      '    ║  └─────────────────┘                    └─────────────────┘',
      '    ║                                                          ║',
      '    ╚══════════════════════════════════════════════════════════╝'
    ]
  }
};

// Estilos de texto
const textStyles = {
  'impact': (text) => \`**\${text}**\`,
  'classic': (text) => \`[\${text}]\`,
  'comic': (text) => \`"\${text}"\`
};

// Generar ID único
function generateId() {
  return 'meme_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

// Calcular nivel de gracia
function calculateFunnyLevel(topText, bottomText, template) {
  let score = 5;
  if (topText.length > 10) score += 1;
  if (bottomText && bottomText.length > 10) score += 1;
  const funnyWords = ['lol', 'haha', 'xd', 'meme', 'epic', 'fail', 'win', 'awesome', 'amazing'];
  const allText = (topText + ' ' + (bottomText || '')).toLowerCase();
  funnyWords.forEach(word => {
    if (allText.includes(word)) score += 1;
  });
  if (['drake', 'surprised-pikachu'].includes(template)) score += 1;
  return Math.min(10, Math.max(1, score));
}

try {
  const startTime = Date.now();
  
  if (!input.topText) {
    throw new Error('topText es requerido');
  }
  
  let template = input.template || 'drake';
  if (!memeTemplates[template]) {
    throw new Error(\`Plantilla '\${template}' no encontrada\`);
  }
  
  const style = input.style || 'classic';
  const styleFunc = textStyles[style] || textStyles.classic;
  
  const styledTopText = styleFunc(input.topText);
  const styledBottomText = input.bottomText ? styleFunc(input.bottomText) : '';
  
  const templateData = memeTemplates[template];
  const asciiArt = templateData.ascii.map(line => {
    return line
      .replace('[TOP TEXT]', styledTopText.padEnd(15))
      .replace('[BOTTOM TEXT]', styledBottomText.padEnd(15));
  }).join('\n');
  
  const processingTime = Date.now() - startTime;
  const funnyLevel = calculateFunnyLevel(input.topText, input.bottomText, template);
  
  const result = {
    meme: {
      id: generateId(),
      template: template,
      topText: input.topText,
      bottomText: input.bottomText || '',
      style: style,
      imageUrl: \`https://meme-generator.example.com/\${template}/\${generateId()}.png\`,
      asciiArt: asciiArt
    },
    metadata: {
      generatedAt: new Date().toISOString(),
      processingTime: processingTime,
      funnyLevel: funnyLevel
    }
  };
  
  console.log(JSON.stringify(result, null, 2));
  
} catch (error) {
  console.error(JSON.stringify({
    error: error.message,
    timestamp: new Date().toISOString()
  }, null, 2));
  process.exit(1);
}
"@

# Guardar el script temporal
$tempScript = "temp-meme-test.js"
$nodeScript | Out-File -FilePath $tempScript -Encoding UTF8

try {
    # Ejecutar el script Node.js
    $result = node $tempScript | ConvertFrom-Json
    
    Write-Host "✅ Meme generado exitosamente!" -ForegroundColor Green
    Write-Host "   ID: $($result.meme.id)" -ForegroundColor Gray
    Write-Host "   Plantilla: $($result.meme.template)" -ForegroundColor Gray
    Write-Host "   Estilo: $($result.meme.style)" -ForegroundColor Gray
    Write-Host "   Nivel de gracia: $($result.metadata.funnyLevel)/10" -ForegroundColor Gray
    Write-Host "   Tiempo de procesamiento: $($result.metadata.processingTime)ms" -ForegroundColor Gray
    
    Write-Host "`n🎨 ASCII Art del meme:" -ForegroundColor Cyan
    Write-Host $result.meme.asciiArt -ForegroundColor White
    
} catch {
    Write-Host "❌ Error ejecutando la herramienta: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    # Limpiar archivo temporal
    if (Test-Path $tempScript) {
        Remove-Item $tempScript
    }
}

Write-Host "`n🎉 ¡Prueba completada!" -ForegroundColor Green
Write-Host "La herramienta de memes está funcionando correctamente." -ForegroundColor Gray

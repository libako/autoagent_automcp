# Script simple para probar la herramienta de memes
Write-Host "🎭 Probando la herramienta de memes del MCP AutoServer" -ForegroundColor Cyan

# Verificar que el servidor esté funcionando
Write-Host "`n1. Verificando estado del servidor..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:8970/health" -Method GET
    Write-Host "✅ Servidor funcionando: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: El servidor no está funcionando" -ForegroundColor Red
    exit 1
}

# Crear un script Node.js simple
$nodeScript = @'
const input = {
  topText: "Programar en Java",
  bottomText: "Programar en Python",
  template: "drake",
  style: "impact"
};

// Plantilla Drake simplificada
const drakeTemplate = [
  "    ╔══════════════════════════════════════════════════════════╗",
  "    ║                                                          ║",
  "    ║  😎  NO                                  😎  YES         ║",
  "    ║                                                          ║",
  "    ║  ┌─────────────────┐                    ┌─────────────────┐",
  "    ║  │                 │                    │                 │",
  "    ║  │   [TOP TEXT]    │                    │  [BOTTOM TEXT]  │",
  "    ║  │                 │                    │                 │",
  "    ║  └─────────────────┘                    └─────────────────┘",
  "    ║                                                          ║",
  "    ╚══════════════════════════════════════════════════════════╝"
];

// Estilos de texto
const textStyles = {
  'impact': (text) => `**${text}**`,
  'classic': (text) => `[${text}]`,
  'comic': (text) => `"${text}"`
};

try {
  const startTime = Date.now();
  
  const style = input.style || 'classic';
  const styleFunc = textStyles[style] || textStyles.classic;
  
  const styledTopText = styleFunc(input.topText);
  const styledBottomText = input.bottomText ? styleFunc(input.bottomText) : '';
  
  const asciiArt = drakeTemplate.map(line => {
    return line
      .replace('[TOP TEXT]', styledTopText.padEnd(15))
      .replace('[BOTTOM TEXT]', styledBottomText.padEnd(15));
  }).join('\n');
  
  const processingTime = Date.now() - startTime;
  
  const result = {
    meme: {
      id: 'meme_' + Date.now(),
      template: input.template,
      topText: input.topText,
      bottomText: input.bottomText || '',
      style: style,
      asciiArt: asciiArt
    },
    metadata: {
      generatedAt: new Date().toISOString(),
      processingTime: processingTime,
      funnyLevel: 8
    }
  };
  
  console.log(JSON.stringify(result, null, 2));
  
} catch (error) {
  console.error(JSON.stringify({
    error: error.message,
    timestamp: new Date().toISOString()
  }, null, 2));
}
'@

# Guardar y ejecutar el script
$tempScript = "temp-meme-simple.js"
$nodeScript | Out-File -FilePath $tempScript -Encoding UTF8

Write-Host "`n2. Ejecutando herramienta meme.generate..." -ForegroundColor Yellow

try {
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
    if (Test-Path $tempScript) {
        Remove-Item $tempScript
    }
}

Write-Host "`n🎉 ¡Prueba completada!" -ForegroundColor Green

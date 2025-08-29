#!/usr/bin/env node

const readline = require('readline');

// Configurar para leer desde stdin
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

// Plantillas de memes en ASCII
const memeTemplates = {
  'drake': {
    name: 'Drake Hotline Bling',
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
  },
  'distracted-boyfriend': {
    name: 'Distracted Boyfriend',
    ascii: [
      '    ╔══════════════════════════════════════════════════════════╗',
      '    ║                                                          ║',
      '    ║  👨 [TOP TEXT]                    👩 [BOTTOM TEXT]      ║',
      '    ║     👀                                                      ║',
      '    ║                                                          ║',
      '    ║  ┌─────────────────┐                    ┌─────────────────┐',
      '    ║  │                 │                    │                 │',
      '    ║  │   [TOP TEXT]    │                    │  [BOTTOM TEXT]  │',
      '    ║  │                 │                    │                 │',
      '    ║  └─────────────────┘                    └─────────────────┘',
      '    ║                                                          ║',
      '    ╚══════════════════════════════════════════════════════════╝'
    ]
  },
  'two-buttons': {
    name: 'Two Buttons',
    ascii: [
      '    ╔══════════════════════════════════════════════════════════╗',
      '    ║                                                          ║',
      '    ║  ┌─────────────────┐    ┌─────────────────┐              ║',
      '    ║  │                 │    │                 │              ║',
      '    ║  │   [TOP TEXT]    │    │  [BOTTOM TEXT]  │              ║',
      '    ║  │                 │    │                 │              ║',
      '    ║  └─────────────────┘    └─────────────────┘              ║',
      '    ║                                                          ║',
      '    ║  🤔 ¿Cuál presionar? 🤔                                   ║',
      '    ║                                                          ║',
      '    ╚══════════════════════════════════════════════════════════╝'
    ]
  },
  'change-my-mind': {
    name: 'Change My Mind',
    ascii: [
      '    ╔══════════════════════════════════════════════════════════╗',
      '    ║                                                          ║',
      '    ║  ┌────────────────────────────────────────────────────┐  ║',
      '    ║  │                                                    │  ║',
      '    ║  │  [TOP TEXT]                                        │  ║',
      '    ║  │                                                    │  ║',
      '    ║  └────────────────────────────────────────────────────┘  ║',
      '    ║                                                          ║',
      '    ║  🪑 Change my mind                                     ║',
      '    ║                                                          ║',
      '    ╚══════════════════════════════════════════════════════════╝'
    ]
  },
  'surprised-pikachu': {
    name: 'Surprised Pikachu',
    ascii: [
      '    ╔══════════════════════════════════════════════════════════╗',
      '    ║                                                          ║',
      '    ║  [TOP TEXT]                                             ║',
      '    ║                                                          ║',
      '    ║  ╭────────────────────────────────────────────────────╮  ║',
      '    ║  │                                                    │  ║',
      '    ║  │  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  ╭─╮  │  ║',
      '    ║  │  │P│  │I│  │K│  │A│  │C│  │H│  │U│  │!│  │!│  │!│  │  ║',
      '    ║  │  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  ╰─╯  │  ║',
      '    ║  │                                                    │  ║',
      '    ║  ╰────────────────────────────────────────────────────╯  ║',
      '    ║                                                          ║',
      '    ╚══════════════════════════════════════════════════════════╝'
    ]
  }
};

// Estilos de texto
const textStyles = {
  'classic': (text) => `[${text}]`,
  'impact': (text) => `**${text}**`,
  'comic': (text) => `"${text}"`,
  'random': (text) => {
    const styles = ['classic', 'impact', 'comic'];
    const randomStyle = styles[Math.floor(Math.random() * styles.length)];
    return textStyles[randomStyle](text);
  }
};

// Generar ID único
function generateId() {
  return 'meme_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

// Calcular nivel de gracia (algoritmo muy científico 😄)
function calculateFunnyLevel(topText, bottomText, template) {
  let score = 5; // Base
  
  // Más texto = más gracioso (hasta cierto punto)
  if (topText.length > 10) score += 1;
  if (bottomText && bottomText.length > 10) score += 1;
  
  // Palabras clave que aumentan la gracia
  const funnyWords = ['lol', 'haha', 'xd', 'meme', 'epic', 'fail', 'win', 'awesome', 'amazing'];
  const allText = (topText + ' ' + (bottomText || '')).toLowerCase();
  
  funnyWords.forEach(word => {
    if (allText.includes(word)) score += 1;
  });
  
  // Plantillas populares son más graciosas
  const popularTemplates = ['drake', 'surprised-pikachu'];
  if (popularTemplates.includes(template)) score += 1;
  
  return Math.min(10, Math.max(1, score));
}

// Procesar la entrada
let inputData = '';

rl.on('line', (line) => {
  inputData += line;
});

rl.on('close', () => {
  try {
    const startTime = Date.now();
    const input = JSON.parse(inputData);
    
    // Validar entrada
    if (!input.topText) {
      throw new Error('topText es requerido');
    }
    
    // Seleccionar plantilla
    let template = input.template || 'random';
    if (template === 'random') {
      const templates = Object.keys(memeTemplates);
      template = templates[Math.floor(Math.random() * templates.length)];
    }
    
    if (!memeTemplates[template]) {
      throw new Error(`Plantilla '${template}' no encontrada`);
    }
    
    // Seleccionar estilo
    const style = input.style || 'classic';
    const styleFunc = textStyles[style] || textStyles.classic;
    
    // Aplicar estilo al texto
    const styledTopText = styleFunc(input.topText);
    const styledBottomText = input.bottomText ? styleFunc(input.bottomText) : '';
    
    // Generar ASCII art
    const templateData = memeTemplates[template];
    const asciiArt = templateData.ascii.map(line => {
      return line
        .replace('[TOP TEXT]', styledTopText.padEnd(15))
        .replace('[BOTTOM TEXT]', styledBottomText.padEnd(15));
    }).join('\n');
    
    // Calcular métricas
    const processingTime = Date.now() - startTime;
    const funnyLevel = calculateFunnyLevel(input.topText, input.bottomText, template);
    
    // Construir respuesta
    const result = {
      meme: {
        id: generateId(),
        template: template,
        topText: input.topText,
        bottomText: input.bottomText || '',
        style: style,
        imageUrl: `https://meme-generator.example.com/${template}/${generateId()}.png`,
        asciiArt: asciiArt
      },
      metadata: {
        generatedAt: new Date().toISOString(),
        processingTime: processingTime,
        funnyLevel: funnyLevel
      }
    };
    
    // Enviar resultado
    console.log(JSON.stringify(result, null, 2));
    
  } catch (error) {
    console.error(JSON.stringify({
      error: error.message,
      timestamp: new Date().toISOString()
    }, null, 2));
    process.exit(1);
  }
});

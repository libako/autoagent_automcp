#!/usr/bin/env node

const readline = require('readline');

// Configurar para leer desde stdin
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

// Base de datos de plantillas
const templates = [
  {
    name: 'drake',
    description: 'Drake Hotline Bling - Comparación entre dos opciones',
    category: 'comparison',
    popularity: 9,
    example: {
      topText: 'Programar en Java',
      bottomText: 'Programar en Python'
    }
  },
  {
    name: 'distracted-boyfriend',
    description: 'Novio distraído mirando a otra chica',
    category: 'reaction',
    popularity: 8,
    example: {
      topText: 'Mi trabajo actual',
      bottomText: 'Una nueva oportunidad'
    }
  },
  {
    name: 'two-buttons',
    description: 'Dos botones - Dilema de elección',
    category: 'dilemma',
    popularity: 7,
    example: {
      topText: 'Estudiar para el examen',
      bottomText: 'Ver Netflix'
    }
  },
  {
    name: 'change-my-mind',
    description: 'Change My Mind - Opinión controvertida',
    category: 'opinion',
    popularity: 6,
    example: {
      topText: 'Pineapple belongs on pizza'
    }
  },
  {
    name: 'surprised-pikachu',
    description: 'Pikachu sorprendido - Reacción exagerada',
    category: 'reaction',
    popularity: 10,
    example: {
      topText: 'Me sorprende que me sorprenda'
    }
  }
];

// Procesar la entrada
let inputData = '';

rl.on('line', (line) => {
  inputData += line;
});

rl.on('close', () => {
  try {
    const startTime = Date.now();
    
    // Construir respuesta
    const result = {
      templates: templates,
      total: templates.length,
      metadata: {
        generatedAt: new Date().toISOString(),
        processingTime: Date.now() - startTime,
        categories: [...new Set(templates.map(t => t.category))],
        averagePopularity: templates.reduce((sum, t) => sum + t.popularity, 0) / templates.length
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

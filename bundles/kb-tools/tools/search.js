#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

// Leer entrada JSON desde stdin
let input = '';
process.stdin.on('data', chunk => {
    input += chunk;
});

process.stdin.on('end', () => {
    try {
        const args = JSON.parse(input);
        const result = searchDocuments(args);
        console.log(JSON.stringify(result, null, 2));
    } catch (error) {
        console.error(JSON.stringify({
            error: error.message,
            stack: error.stack
        }));
        process.exit(1);
    }
});

function searchDocuments(args) {
    const { query, limit = 10, filters = {} } = args;
    const startTime = Date.now();
    
    // Simular base de datos de documentos
    const documents = [
        {
            id: "doc-001",
            title: "Introducción a MCP",
            content: "El Model Context Protocol (MCP) es un protocolo para conectar agentes de IA con herramientas y datos externos.",
            category: "documentation",
            createdAt: "2024-01-15T10:00:00Z",
            score: 0.95
        },
        {
            id: "doc-002", 
            title: "Guía de implementación de herramientas",
            content: "Esta guía explica cómo implementar herramientas personalizadas para el protocolo MCP.",
            category: "tutorial",
            createdAt: "2024-01-20T14:30:00Z",
            score: 0.87
        },
        {
            id: "doc-003",
            title: "Mejores prácticas de seguridad",
            content: "Documento sobre las mejores prácticas de seguridad al implementar servidores MCP.",
            category: "security",
            createdAt: "2024-01-25T09:15:00Z",
            score: 0.72
        }
    ];
    
    // Filtrar por consulta (búsqueda simple)
    let results = documents.filter(doc => 
        doc.title.toLowerCase().includes(query.toLowerCase()) ||
        doc.content.toLowerCase().includes(query.toLowerCase())
    );
    
    // Aplicar filtros adicionales
    if (filters.category) {
        results = results.filter(doc => doc.category === filters.category);
    }
    
    if (filters.dateFrom) {
        const dateFrom = new Date(filters.dateFrom);
        results = results.filter(doc => new Date(doc.createdAt) >= dateFrom);
    }
    
    if (filters.dateTo) {
        const dateTo = new Date(filters.dateTo);
        results = results.filter(doc => new Date(doc.createdAt) <= dateTo);
    }
    
    // Ordenar por relevancia y limitar resultados
    results.sort((a, b) => b.score - a.score);
    results = results.slice(0, limit);
    
    const executionTime = Date.now() - startTime;
    
    return {
        results,
        total: results.length,
        query,
        executionTime
    };
}

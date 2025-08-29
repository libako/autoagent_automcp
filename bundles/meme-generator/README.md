# 🎭 Meme Generator Bundle

Un bundle divertido para generar memes en ASCII art usando el MCP AutoServer.

## 🚀 Características

- **5 plantillas populares**: Drake, Distracted Boyfriend, Two Buttons, Change My Mind, Surprised Pikachu
- **Estilos de texto**: Classic, Impact, Comic, Random
- **ASCII Art**: Generación de memes en formato texto para consola
- **Algoritmo de gracia**: Calcula automáticamente el nivel de gracia del meme (1-10)
- **Múltiples categorías**: Comparison, Reaction, Dilemma, Opinion

## 🛠️ Herramientas Disponibles

### `meme.generate`
Genera un meme personalizado basado en una plantilla.

**Parámetros:**
- `topText` (requerido): Texto superior del meme
- `bottomText` (opcional): Texto inferior del meme
- `template` (opcional): Plantilla a usar (drake, distracted-boyfriend, two-buttons, change-my-mind, surprised-pikachu, random)
- `style` (opcional): Estilo del texto (classic, impact, comic, random)

**Ejemplo de uso:**
```json
{
  "topText": "Programar en Java",
  "bottomText": "Programar en Python",
  "template": "drake",
  "style": "impact"
}
```

### `meme.templates`
Lista todas las plantillas disponibles con ejemplos.

**Ejemplo de respuesta:**
```json
{
  "templates": [
    {
      "name": "drake",
      "description": "Drake Hotline Bling - Comparación entre dos opciones",
      "category": "comparison",
      "popularity": 9,
      "example": {
        "topText": "Programar en Java",
        "bottomText": "Programar en Python"
      }
    }
  ],
  "total": 5
}
```

## 📁 Estructura del Bundle

```
meme-generator/
├── bundle.json              # Manifiesto del bundle
├── README.md               # Este archivo
├── schemas/                # Esquemas JSON
│   ├── generate.in.json    # Esquema de entrada para generate
│   ├── generate.out.json   # Esquema de salida para generate
│   ├── templates.out.json  # Esquema de salida para templates
│   └── template.schema.json # Esquema del recurso templates
└── tools/                  # Herramientas Node.js
    ├── generate.js         # Generador de memes
    └── templates.js        # Listador de plantillas
```

## 🎨 Plantillas Disponibles

### 1. Drake Hotline Bling
- **Categoría**: Comparison
- **Popularidad**: 9/10
- **Uso**: Comparar dos opciones o situaciones

### 2. Distracted Boyfriend
- **Categoría**: Reaction
- **Popularidad**: 8/10
- **Uso**: Mostrar distracción o cambio de interés

### 3. Two Buttons
- **Categoría**: Dilemma
- **Popularidad**: 7/10
- **Uso**: Presentar un dilema o elección difícil

### 4. Change My Mind
- **Categoría**: Opinion
- **Popularidad**: 6/10
- **Uso**: Expresar una opinión controvertida

### 5. Surprised Pikachu
- **Categoría**: Reaction
- **Popularidad**: 10/10
- **Uso**: Reacción exagerada a algo obvio

## 🎯 Ejemplos de Uso

### Meme de Drake
```json
{
  "topText": "Debuggear código",
  "bottomText": "Escribir código desde cero",
  "template": "drake",
  "style": "impact"
}
```

### Meme de Pikachu
```json
{
  "topText": "El código que escribí a las 3 AM tiene bugs",
  "template": "surprised-pikachu",
  "style": "comic"
}
```

### Meme de Two Buttons
```json
{
  "topText": "Estudiar para el examen",
  "bottomText": "Ver Netflix",
  "template": "two-buttons",
  "style": "classic"
}
```

## 🔧 Instalación

1. Asegúrate de que Node.js esté instalado en el sistema
2. El bundle se carga automáticamente cuando el MCP AutoServer detecta el directorio
3. Las herramientas estarán disponibles como `meme.generate` y `meme.templates`

## 🎪 Algoritmo de Gracia

El sistema incluye un algoritmo "científico" que calcula el nivel de gracia del meme (1-10):

- **Base**: 5 puntos
- **Texto largo**: +1 punto por texto >10 caracteres
- **Palabras graciosas**: +1 punto por palabra (lol, haha, xd, meme, epic, fail, win, awesome, amazing)
- **Plantillas populares**: +1 punto para drake y surprised-pikachu

## 🚀 Próximas Características

- [ ] Más plantillas de memes
- [ ] Generación de imágenes reales
- [ ] Efectos de texto personalizables
- [ ] Memes animados en ASCII
- [ ] Integración con APIs de memes populares

---

*¡Disfruta generando memes divertidos con el MCP AutoServer! 🎉*

#!/usr/bin/env node

interface ToolArgs {
  input: string;
  options?: {
    uppercase?: boolean;
    reverse?: boolean;
  };
}

interface ToolResult {
  output: string;
  processed: boolean;
  timestamp: string;
}

function processTool(args: ToolArgs): ToolResult {
  let output = args.input;
  
  if (args.options?.uppercase) {
    output = output.toUpperCase();
  }
  
  if (args.options?.reverse) {
    output = output.split('').reverse().join('');
  }
  
  return {
    output,
    processed: true,
    timestamp: new Date().toISOString()
  };
}

// Leer entrada desde stdin
let input = '';
process.stdin.on('data', (chunk) => {
  input += chunk;
});

process.stdin.on('end', () => {
  try {
    const args: ToolArgs = JSON.parse(input);
    const result = processTool(args);
    console.log(JSON.stringify(result, null, 2));
  } catch (error) {
    console.error(JSON.stringify({
      error: error instanceof Error ? error.message : 'Unknown error',
      processed: false
    }));
    process.exit(1);
  }
});

// Manejar errores de stdin
process.stdin.on('error', (error) => {
  console.error(JSON.stringify({
    error: error.message,
    processed: false
  }));
  process.exit(1);
});

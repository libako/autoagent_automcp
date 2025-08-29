use wasm_bindgen::prelude::*;
use serde::{Deserialize, Serialize};

#[derive(Deserialize)]
struct ToolArgs {
    input: String,
    options: Option<ToolOptions>,
}

#[derive(Deserialize)]
struct ToolOptions {
    uppercase: Option<bool>,
    reverse: Option<bool>,
}

#[derive(Serialize)]
struct ToolResult {
    output: String,
    processed: bool,
    timestamp: String,
}

#[wasm_bindgen]
pub fn process_tool(input: &str) -> String {
    console_error_panic_hook::set_once();
    
    match serde_json::from_str::<ToolArgs>(input) {
        Ok(args) => {
            let mut output = args.input;
            
            if let Some(options) = args.options {
                if options.uppercase.unwrap_or(false) {
                    output = output.to_uppercase();
                }
                
                if options.reverse.unwrap_or(false) {
                    output = output.chars().rev().collect();
                }
            }
            
            let result = ToolResult {
                output,
                processed: true,
                timestamp: js_sys::Date::new_0().to_iso_string().as_string().unwrap_or_default(),
            };
            
            serde_json::to_string(&result).unwrap_or_else(|_| {
                serde_json::to_string(&ToolResult {
                    output: "Error serializing result".to_string(),
                    processed: false,
                    timestamp: String::new(),
                }).unwrap()
            })
        }
        Err(_) => {
            let error_result = ToolResult {
                output: "Invalid input JSON".to_string(),
                processed: false,
                timestamp: String::new(),
            };
            serde_json::to_string(&error_result).unwrap()
        }
    }
}

#[wasm_bindgen]
pub fn greet(name: &str) -> String {
    format!("Hello, {}!", name)
}

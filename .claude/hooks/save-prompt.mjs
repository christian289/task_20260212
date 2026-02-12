#!/usr/bin/env node

/**
 * UserPromptSubmit Hook: Save user's prompt for auto-commit
 * Reads the prompt from stdin JSON and saves it to a file
 */

import { writeFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';

const PROMPT_FILE = join(dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '.last_prompt');

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) {
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString('utf-8');
}

function extractPrompt(input) {
  // 1. Standard JSON parse - handles escaped \n correctly
  try {
    const data = JSON.parse(input);
    if (data.prompt) return data.prompt;
    if (data.content) return data.content;
    if (data.text) return data.text;
    if (data.message) return data.message;
  } catch {}

  // 2. Fallback: extract JSON string value with proper escape handling
  //    Supports escaped chars like \n, \t, \", \\
  try {
    const match = input.match(/"(?:prompt|content|text)"\s*:\s*"((?:[^"\\]|\\.)*)"/s);
    if (match) {
      // Unescape JSON string escapes (e.g., \\n -> \n)
      return JSON.parse(`"${match[1]}"`);
    }
  } catch {}

  return null;
}

async function main() {
  try {
    const input = await readStdin();
    if (!input.trim()) process.exit(0);

    const prompt = extractPrompt(input);
    if (!prompt) process.exit(0);

    // Preserve newlines from multi-line prompts (notepad input)
    const cleanPrompt = prompt.replace(/\r\n/g, '\n').trim();

    mkdirSync(dirname(PROMPT_FILE), { recursive: true });
    writeFileSync(PROMPT_FILE, cleanPrompt, 'utf-8');
  } catch (err) {
    // Silent fail - don't block user interaction
    process.exit(0);
  }
}

main();

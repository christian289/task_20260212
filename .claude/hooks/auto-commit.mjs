#!/usr/bin/env node

/**
 * Stop Hook: Auto-commit changes with user's prompt as commit message
 * Reads the saved prompt, checks for changes, and commits
 */

import { readFileSync, writeFileSync, unlinkSync, existsSync } from 'fs';
import { join, dirname, resolve } from 'path';
import { execSync } from 'child_process';
import { tmpdir } from 'os';

const HOOKS_DIR = dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1'));
const PROMPT_FILE = join(HOOKS_DIR, '.last_prompt');

function getProjectRoot() {
  try {
    return execSync('git rev-parse --show-toplevel', { encoding: 'utf-8' }).trim();
  } catch {
    return process.cwd();
  }
}

function hasChanges(cwd) {
  try {
    const status = execSync('git status --porcelain', { cwd, encoding: 'utf-8' }).trim();
    return status.length > 0;
  } catch {
    return false;
  }
}

function formatCommitMessage(prompt) {
  // First line: truncated prompt (max 72 chars)
  const firstLine = prompt.length > 72
    ? prompt.substring(0, 69) + '...'
    : prompt;

  // If prompt was truncated, include full text in body
  if (prompt.length > 72) {
    return `${firstLine}\n\n[User Request]\n${prompt}`;
  }

  return firstLine;
}

async function main() {
  try {
    if (!existsSync(PROMPT_FILE)) {
      process.exit(0);
    }

    const prompt = readFileSync(PROMPT_FILE, 'utf-8').trim();

    // Clean up prompt file
    try { unlinkSync(PROMPT_FILE); } catch {}

    if (!prompt) {
      process.exit(0);
    }

    const projectRoot = getProjectRoot();

    if (!hasChanges(projectRoot)) {
      process.exit(0);
    }

    const commitMessage = formatCommitMessage(prompt);

    // Write commit message to temp file to preserve newlines
    const tmpFile = join(tmpdir(), `claude-commit-${Date.now()}.txt`);
    writeFileSync(tmpFile, commitMessage, 'utf-8');

    try {
      // Stage all changes
      execSync('git add -A', { cwd: projectRoot, encoding: 'utf-8' });

      // Commit using -F (file) to preserve multiline message
      execSync(`git commit -F "${tmpFile}" --no-verify`, {
        cwd: projectRoot,
        encoding: 'utf-8'
      });
    } finally {
      // Clean up temp file
      try { unlinkSync(tmpFile); } catch {}
    }

  } catch (err) {
    // Silent fail - don't block Claude's stop
    process.exit(0);
  }
}

main();

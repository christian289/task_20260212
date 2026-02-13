#!/usr/bin/env node

/**
 * Stop Hook: Auto-commit changes with user's prompt as commit message
 * Reads the saved prompt, checks for changes, and commits
 *
 * Branch-aware behavior:
 * - main/master: Auto-commit with "[User Request]" prefix (사용자 생각 흐름 기록)
 * - feature branches: 사용자 요구사항을 빈 커밋으로 자동 기록 (코드 변경은 Claude가 직접 커밋)
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

function getCurrentBranch(cwd) {
  try {
    return execSync('git rev-parse --abbrev-ref HEAD', { cwd, encoding: 'utf-8' }).trim();
  } catch {
    return 'main';
  }
}

function isMainBranch(cwd) {
  const branch = getCurrentBranch(cwd);
  return branch === 'main' || branch === 'master';
}

function hasChanges(cwd) {
  try {
    const status = execSync('git status --porcelain', { cwd, encoding: 'utf-8' }).trim();
    return status.length > 0;
  } catch {
    return false;
  }
}

function commitWithTmpFile(message, cwd, extraFlags = '') {
  const tmpFile = join(tmpdir(), `claude-commit-${Date.now()}.txt`);
  writeFileSync(tmpFile, message, 'utf-8');
  try {
    execSync(`git commit ${extraFlags} -F "${tmpFile}" --no-verify`, {
      cwd,
      encoding: 'utf-8'
    });
  } finally {
    try { unlinkSync(tmpFile); } catch {}
  }
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

    if (isMainBranch(projectRoot)) {
      // main 브랜치: 기존 동작 - 모든 변경사항을 [User Request]로 커밋
      if (!hasChanges(projectRoot)) {
        process.exit(0);
      }

      const commitMessage = `[User Request]\n${prompt}`;
      execSync('git add -A', { cwd: projectRoot, encoding: 'utf-8' });
      commitWithTmpFile(commitMessage, projectRoot);
    } else {
      // Feature 브랜치: 사용자 요구사항을 빈 커밋으로 기록
      // 코드 변경사항은 커밋하지 않음 (Claude가 의미 있는 단위로 직접 커밋)
      const commitMessage = `[요구사항]\n${prompt}`;
      commitWithTmpFile(commitMessage, projectRoot, '--allow-empty');
    }

  } catch (err) {
    // Silent fail - don't block Claude's stop
    process.exit(0);
  }
}

main();

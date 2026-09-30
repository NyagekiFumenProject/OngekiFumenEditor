// Fixtures for the MCP e2e suites.
//
// Everything the suites touch is generated into `tests/mcp-e2e/.tmp/` (gitignored) so the
// committed chart samples under OngekiFumenEditor.Benchmark/Data/FumenSamples stay pristine
// and the suites never depend on files outside the repository.

import { mkdir, copyFile, writeFile, readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

export const repoRoot = fileURLToPath(new URL('../../../', import.meta.url));
export const moduleRoot = fileURLToPath(new URL('../', import.meta.url));

const SAMPLE_DIR = path.join(repoRoot, 'OngekiFumenEditor.Benchmark', 'Data', 'FumenSamples');

/** Chart used as the general-purpose fixture (copied, never opened in place). */
export const SEED_CHART = path.join(SAMPLE_DIR, '20993_04.ogkr');
/** Second chart, used to prove that two editors can be opened side by side. */
export const SECOND_CHART = path.join(SAMPLE_DIR, '20997_00.ogkr');

export const AUDIO_SECONDS = 30;

/** Write a deterministic 16-bit mono PCM WAV (no external assets required). */
function buildSilentWav(seconds, sampleRate = 44100) {
  const frames = Math.round(seconds * sampleRate);
  const dataBytes = frames * 2;
  const wav = Buffer.alloc(44 + dataBytes);
  wav.write('RIFF', 0, 'ascii');
  wav.writeUInt32LE(36 + dataBytes, 4);
  wav.write('WAVE', 8, 'ascii');
  wav.write('fmt ', 12, 'ascii');
  wav.writeUInt32LE(16, 16);          // PCM chunk size
  wav.writeUInt16LE(1, 20);           // format = PCM
  wav.writeUInt16LE(1, 22);           // channels = mono
  wav.writeUInt32LE(sampleRate, 24);
  wav.writeUInt32LE(sampleRate * 2, 28); // byte rate
  wav.writeUInt16LE(2, 32);           // block align
  wav.writeUInt16LE(16, 34);          // bits per sample
  wav.write('data', 36, 'ascii');
  wav.writeUInt32LE(dataBytes, 40);
  // Sample data stays zero (silence): the suites never play audio back.
  return wav;
}

/** Minimal `.nyagekiProj` as produced by EditorProjectDataModelSerializer_Latest. */
function buildProjectFile({ audioPath, fumenPath }) {
  return JSON.stringify({
    Version: '0.5.4',
    // must be a well-formed GUID: the model binds `Id` to System.Guid, and a malformed
    // value makes the whole deserialization throw, silently yielding an empty project.
    Id: '00000000-0000-0000-0000-0000000000e2',
    AudioFilePath: audioPath,
    AudioDuration: { Ticks: AUDIO_SECONDS * 10_000_000 },
    FumenFilePath: fumenPath,
    RememberLastDisplayTime: { Ticks: 0 },
    StoreBulletPalleteEditorDatas: {},
  }, null, 2);
}

/**
 * Materialise every generated fixture and return the resulting paths.
 * Safe to call repeatedly; existing files are rewritten.
 */
export async function prepareFixtures({ force = false } = {}) {
  const workDir = path.join(moduleRoot, '.tmp');
  await mkdir(workDir, { recursive: true });

  const audioPath = path.join(workDir, `silence-${AUDIO_SECONDS}s.wav`);
  if (force || !existsSync(audioPath)) {
    await writeFile(audioPath, buildSilentWav(AUDIO_SECONDS));
  }

  const seededChart = path.join(workDir, 'seed.ogkr');
  await copyFile(SEED_CHART, seededChart);

  const secondChart = path.join(workDir, 'second.ogkr');
  await copyFile(SECOND_CHART, secondChart);

  const projectPath = path.join(workDir, 'fixture.nyagekiProj');
  await writeFile(projectPath, buildProjectFile({ audioPath, fumenPath: seededChart }));

  return {
    workDir,
    audioPath,
    seededChart,
    secondChart,
    projectPath,
    seedChart: SEED_CHART,
  };
}

/** Re-read a fixture chart's raw text (used to prove the seeded sample is left untouched). */
export function readText(filePath) {
  return readFile(filePath, 'utf8');
}

export const totalGridResolution = 1920;

/** Split an absolute total-grid into the (tGridUnit, tGridGrid) pair the tools expect. */
export function splitTotalGrid(totalGrid) {
  const unit = Math.floor(totalGrid / totalGridResolution);
  return { tGridUnit: unit, tGridGrid: totalGrid - unit * totalGridResolution };
}

/**
 * Parse a `T[unit,grid]` string (as produced by `TGrid.ToString()`) back into an absolute
 * total-grid. Returns `null` when the string is not a TGrid literal.
 */
export function parseTGridTotal(text) {
  const match = /^T\[(-?\d+(?:\.\d+)?),(-?\d+)\]$/.exec(String(text));
  if (!match) return null;
  return Math.round(Number(match[1]) * totalGridResolution) + Number(match[2]);
}

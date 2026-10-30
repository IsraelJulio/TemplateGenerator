#!/usr/bin/env node
/**
 * Verificacao da regra de versao da ADR-0009 (T10). FALHA (exit 1) se alguma
 * dependencia do package.json fora da lista de excecao tiver intervalo
 * (^, ~, >, <, *, x, ||, faixa com hifen, tag, URL...) em vez de versao exata.
 * Tambem confere que o lockfile resolve exatamente a versao fixada.
 *
 * Uso: node scripts/check-versions.mjs [--package <package.json>] [--lock <package-lock.json>]
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && process.argv[i + 1] ? resolve(process.argv[i + 1]) : fallback;
}

const pkgPath = arg('--package', resolve(here, '../package.json'));
const lockPath = arg('--lock', resolve(here, '../package-lock.json'));
const pkg = JSON.parse(readFileSync(pkgPath, 'utf8'));
const lock = existsSync(lockPath) ? JSON.parse(readFileSync(lockPath, 'utf8')) : undefined;

/** ADR-0009, "Escopo do intervalo": o conjunto em que `ng update` atua. Mudar e editar a ADR. */
const RANGE_EXCEPTIONS = [
  /^@angular\//,
  /^@angular-devkit\//,
  /^@schematics\//,
  /^typescript$/,
  /^rxjs$/,
  /^zone\.js$/,
  /^tslib$/,
];

/** Versao exata: MAJOR.MINOR.PATCH, com pre-release opcional. Nada de operador. */
const EXACT = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/;

const violations = [];
let checked = 0;
let exempt = 0;

for (const section of [
  'dependencies',
  'devDependencies',
  'optionalDependencies',
  'peerDependencies',
]) {
  for (const [name, spec] of Object.entries(pkg[section] ?? {})) {
    if (RANGE_EXCEPTIONS.some((re) => re.test(name))) {
      exempt += 1;
      continue;
    }
    checked += 1;
    if (!EXACT.test(spec)) {
      violations.push(
        `${section}.${name}: "${spec}" e intervalo; fora da lista de excecao da ADR-0009 a versao tem de ser exata`,
      );
      continue;
    }
    const resolved = lock?.packages?.[`node_modules/${name}`]?.version;
    if (resolved !== undefined && resolved !== spec) {
      violations.push(
        `${section}.${name}: package.json fixa ${spec}, mas o lockfile resolve ${resolved}`,
      );
    }
  }
}

if (violations.length > 0) {
  console.error(`check:versions FALHOU (${violations.length})`);
  for (const v of violations) console.error(`  - ${v}`);
  process.exit(1);
}
console.log(
  `check:versions OK: ${checked} dependencias com versao exata, ${exempt} na lista de excecao da ADR-0009`,
);

#!/usr/bin/env node
/**
 * Prova que os dois verificadores FALHAM diante do caso ruim (T10): roda cada
 * um contra um fixture e exige exit != 0 e a mencao ao culpado esperado.
 */
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const cases = [
  {
    script: 'check-licenses.mjs',
    args: ['--lock', resolve(here, 'fixtures/bad-lock.json')],
    expect: ['gpl-thing', 'nolic', 'lightningcss', 'paren-trap', 'broken-expr', 'with-expr'],
    forbid: ['paren-ok'],
  },
  {
    script: 'check-versions.mjs',
    args: ['--package', resolve(here, 'fixtures/bad-package.json')],
    expect: [
      'left-pad',
      'vitest',
      'devDependencies.a:',
      'devDependencies.b:',
      'devDependencies.c:',
      'devDependencies.d:',
    ],
  },
];

let failed = false;
for (const c of cases) {
  const r = spawnSync(process.execPath, [resolve(here, c.script), ...c.args], { encoding: 'utf8' });
  const out = `${r.stdout}${r.stderr}`;
  const missing = c.expect.filter((s) => !out.includes(s));
  const wrongly = (c.forbid ?? []).filter((s) => out.includes(s));
  const ok =
    r.status !== 0 &&
    missing.length === 0 &&
    wrongly.length === 0 &&
    !out.includes('@angular/core') &&
    !out.includes('devDependencies.e');
  console.log(
    `${ok ? 'OK ' : 'ERRO'} ${c.script} saiu com ${r.status} no caso ruim${missing.length ? `; faltou acusar: ${missing.join(', ')}` : ''}${wrongly.length ? `; acusou indevidamente: ${wrongly.join(', ')}` : ''}`,
  );
  if (!ok) {
    failed = true;
    console.log(out);
  }
}
process.exit(failed ? 1 : 0);

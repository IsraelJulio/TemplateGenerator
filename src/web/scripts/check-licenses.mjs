#!/usr/bin/env node
/**
 * Auditoria de licencas npm (RNF-09, T10). FALHA (exit 1) se algum pacote do
 * package-lock.json tiver licenca fora da lista permitida, sem licenca
 * declarada, ou se uma excecao nao se aplicar.
 *
 * Uso: node scripts/check-licenses.mjs [--lock <package-lock.json>] [--policy <license-policy.json>]
 * Sem dependencias: so le o lockfile (que e o que `npm ci` instala).
 */
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && process.argv[i + 1] ? resolve(process.argv[i + 1]) : fallback;
}

const lockPath = arg('--lock', resolve(here, '../package-lock.json'));
const policyPath = arg('--policy', resolve(here, 'license-policy.json'));
const lock = JSON.parse(readFileSync(lockPath, 'utf8'));
const policy = JSON.parse(readFileSync(policyPath, 'utf8'));
const allowed = new Set(policy.allowed);

/**
 * Expressao SPDX com agrupamento: `OR` basta uma alternativa permitida; `AND` exige todas;
 * `AND` precede `OR`; parenteses agrupam. Falha fechada: `WITH`, operador minusculo ou
 * expressao malformada contam como NAO permitida (cai na lista de excecoes ou vira violacao).
 */
function expressionAllowed(expression) {
  const tokens = expression.match(/\(|\)|[^\s()]+/g) ?? [];
  let pos = 0;
  const peek = () => tokens[pos];
  const fail = () => {
    throw new Error('expressao SPDX nao avaliavel');
  };

  function orExpr() {
    let ok = andExpr();
    while (peek() === 'OR') {
      pos += 1;
      ok = andExpr() || ok;
    }
    return ok;
  }
  function andExpr() {
    let ok = atom();
    while (peek() === 'AND') {
      pos += 1;
      ok = atom() && ok;
    }
    return ok;
  }
  function atom() {
    const t = tokens[pos++];
    if (t === '(') {
      const ok = orExpr();
      if (tokens[pos++] !== ')') fail();
      return ok;
    }
    if (t === undefined || t === ')' || /^(AND|OR|WITH)$/i.test(t)) fail();
    if (peek()?.toUpperCase() === 'WITH') fail();
    return allowed.has(t);
  }

  try {
    const ok = orExpr();
    return pos === tokens.length && ok;
  } catch {
    return false;
  }
}

const nameOf = (path) => path.replace(/^.*node_modules\//, '');
const counts = new Map();
const violations = [];
let total = 0;

for (const [path, info] of Object.entries(lock.packages ?? {})) {
  if (path === '') continue;
  total += 1;
  const license = typeof info.license === 'string' ? info.license : undefined;
  const label = `${nameOf(path)}@${info.version ?? '?'}`;
  const key = license ?? '(ausente)';
  counts.set(key, (counts.get(key) ?? 0) + 1);

  if (!license) {
    violations.push(`${label}: sem licenca declarada no lockfile`);
    continue;
  }
  if (expressionAllowed(license)) continue;

  const exception = (policy.exceptions ?? []).find(
    (e) => e.license === license && new RegExp(e.packagePattern).test(nameOf(path)),
  );
  if (exception) {
    if (exception.devOnly && info.dev !== true) {
      violations.push(
        `${label}: ${license} so e aceita como devDependency, e este pacote nao e dev`,
      );
    }
    continue;
  }
  violations.push(`${label}: licenca "${license}" fora da lista permitida`);
}

const resumo = [...counts]
  .sort((a, b) => b[1] - a[1])
  .map(([l, n]) => `${l}=${n}`)
  .join(', ');
if (violations.length > 0) {
  console.error(`check:licenses FALHOU (${violations.length} de ${total} pacotes)`);
  for (const v of violations) console.error(`  - ${v}`);
  process.exit(1);
}
console.log(`check:licenses OK: ${total} pacotes dentro da lista permitida (${resumo})`);

/**
 * Declaração mínima do que o fluxo ponta a ponta usa do Node, válida **somente
 * dentro de `e2e/`**.
 *
 * Mesmo motivo de `src/app/testing/node-fs.d.ts`: trazer `@types/node` só para
 * ler um arquivo baixado adicionaria uma dependência a registrar em
 * `docs/THIRD-PARTY.md` (RNF-09) sem que nada do código de produção precise
 * dela. `tsconfig.e2e.json` é o único programa que inclui este arquivo, então a
 * declaração não vaza para a aplicação nem para os testes de unidade.
 */
declare module 'node:fs' {
  /** Sem `encoding`, o Node devolve os bytes crus. */
  export function readFileSync(path: string): Uint8Array;
}

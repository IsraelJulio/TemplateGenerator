/**
 * Declaração mínima de `node:fs`, válida **somente nos testes**.
 *
 * Dois testes conferem o que está escrito nos arquivos do próprio repositório:
 * `contrast.spec.ts` recalcula o contraste a partir dos tokens de
 * `src/styles.css`, e `project-structure.spec.ts` varre os arquivos de produção
 * do frontend para provar que os valores de opção continuam confinados ao
 * módulo da projeção. Ambos rodam no Node, sob o Vitest.
 *
 * Declarar as três assinaturas aqui evita trazer `@types/node` como dependência
 * só por causa delas — dependência nova exigiria registro em
 * `docs/THIRD-PARTY.md` (RNF-09) e entraria no `package-lock.json` sem que nada
 * do código de produção precise dela.
 *
 * **O escopo é o que torna isso seguro.** `declare module` é ambiente: se este
 * arquivo entrasse no programa da aplicação, o código de produção passaria a
 * typecheckar um `readFileSync` que não existe no navegador. Ele vive dentro de
 * `src/app/testing/`, que `tsconfig.app.json` exclui e `tsconfig.spec.json`
 * inclui junto com todo `.d.ts` sob `src` — a declaração só vale no programa
 * dos testes, onde há Node de verdade.
 */
declare module 'node:fs' {
  export function readFileSync(path: string, encoding: 'utf8'): string;

  /** O suficiente para percorrer um diretório sem `@types/node`. */
  export interface Dirent {
    readonly name: string;
    isDirectory(): boolean;
  }

  export function readdirSync(path: string, options: { withFileTypes: true }): Dirent[];
}

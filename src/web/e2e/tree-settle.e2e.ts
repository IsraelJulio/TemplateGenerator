import { expect, test } from '@playwright/test';
import { settleAfterChange, type TreeRead } from './support';

/**
 * T09b: a regra de assentamento da árvore, provada **sem navegador**, com
 * leituras roteirizadas.
 *
 * `treeAcross` espera uma leitura diferente da anterior **e** igual à leitura
 * feita imediatamente antes dela. Na tela, uma troca de um evento só vira um
 * render só, e nenhum teste de tela consegue mostrar que a segunda condição é
 * necessária. Numa troca de vários eventos (`chooseNonDefault` e depois o nome,
 * ou o nome digitado tecla a tecla) existe estado intermediário, e a primeira
 * leitura diferente pode ser ele. Aqui o roteiro cria esse estado de propósito.
 *
 * **Por que aqui, e não no Vitest de `ng test`.** O que está sob teste é o
 * `expect.poll` do Playwright: o intervalo entre as leituras, o timeout padrão e
 * a mensagem. `support.ts` importa `@playwright/test`, que o Vitest não roda, e
 * `tsconfig.spec.json` não enxerga `e2e/`. Um teste do Playwright sem a fixture
 * `page` não abre página nem contexto: custa o intervalo do poll, menos de um
 * segundo. Ele roda nos dois projetos, desktop e celular, como tudo em `e2e/`;
 * a duplicata custa outro segundo e evita um `skip` por projeto.
 */

/** Devolve as leituras na ordem e repete a última. Conta quantas foram feitas. */
function scripted(...reads: string[][]): { read: TreeRead; count: () => number } {
  let made = 0;
  return {
    read: async () => reads[Math.min(made++, reads.length - 1)],
    count: () => made,
  };
}

const ANTERIOR = ['src/', 'src/Anterior/'];
const INTERMEDIARIA = ['src/', 'src/Intermediaria/'];
const NOVA = ['src/', 'src/Nova/'];

test('T09b, sem navegador: a primeira leitura diferente não basta; anterior A, leituras B, C, C devolvem C', async () => {
  const roteiro = scripted(INTERMEDIARIA, NOVA, NOVA);

  const assentada = await settleAfterChange(roteiro.read, ANTERIOR);

  expect(assentada, 'devolveu o estado intermediário: falta a condição de estabilidade').toEqual(
    NOVA,
  );
  expect(roteiro.count(), 'leituras até assentar').toBe(3);
});

test('T09b, sem navegador: estabilidade não basta; anterior A, leituras A, A, B, B devolvem B', async () => {
  const roteiro = scripted(ANTERIOR, ANTERIOR, NOVA, NOVA);

  const assentada = await settleAfterChange(roteiro.read, ANTERIOR);

  expect(assentada, 'devolveu a árvore que ainda não mudou: falta a condição de diferença').toEqual(
    NOVA,
  );
  expect(roteiro.count(), 'leituras até assentar').toBe(4);
});

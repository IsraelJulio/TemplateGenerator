import { expect, test } from '@playwright/test';
import {
  expectTreeMatchesZip,
  generateAndOpen,
  openWithProjectName,
  pickSecondOfFirstGroup,
  stableTree,
  treeAfterChange,
} from './support';

/**
 * T09b: a leitura da árvore depois de uma troca, provada nos dois sentidos.
 *
 * Na primeira execução da suíte de T04, a perna celular de "a outra arquitetura
 * do catálogo também baixa o que a tela prometeu" falhou uma vez na comparação
 * árvore × ZIP. O teste fazia a seta, esperava `toBeChecked()` e lia a árvore. Só
 * que o navegador marca o rádio na hora da seta, e o Angular, sem zone.js, deixa
 * o render para um `setTimeout` ou `requestAnimationFrame` depois do evento. A
 * leitura podia pegar a árvore da arquitetura anterior com o ZIP já da nova.
 *
 * Repetir a suíte não prova nada aqui. Os testes abaixo não dependem da sorte:
 *
 * - o primeiro planta a leitura obsoleta e **tem de falhar**;
 * - o sentido 2, o mesmo cenário com `treeAfterChange`, é o próprio teste de
 *   `download.e2e.ts` e tem de passar;
 * - o último para o relógio da página e mostra a corrida acontecendo, sem
 *   depender de quando o render cai.
 */

/**
 * Sentido 1. A árvore lida **antes** da troca, usada como se fosse a prometida,
 * diverge do ZIP da outra arquitetura. Era isso que a leitura obsoleta fazia.
 *
 * O teste é **feito para falhar**, e declara isso com `test.fail()`. O `fail`
 * vem só antes da comparação, de propósito: navegação, troca e download
 * acontecem antes dele, e uma falha ali é falha de verdade, não a esperada. Se a
 * comparação parar de discriminar as duas árvores, o Playwright acusa "expected
 * to fail, but passed" e a suíte fica vermelha.
 */
test('T09b, sentido 1: a árvore lida antes da troca diverge do ZIP da outra arquitetura (falha de propósito)', async ({
  page,
}, testInfo) => {
  const { form, tree: antes } = await openWithProjectName(page);

  await pickSecondOfFirstGroup(page, form);
  const real = await generateAndOpen(page, form, testInfo);

  test.fail(true, 'a leitura obsoleta tem de divergir do ZIP da nova arquitetura');
  expectTreeMatchesZip(antes, real);
});

/**
 * A corrida, reproduzida em vez de esperada. Com o relógio da página parado,
 * nenhum `setTimeout` nem `requestAnimationFrame` dispara, e o render que a seta
 * agendou fica pendente pelo tempo que o teste quiser.
 *
 * Nesse intervalo, `toBeChecked()` já passa e a árvore na tela ainda é a
 * anterior. A estabilidade sozinha aceita essa árvore. `treeAfterChange` recusa,
 * esgota o timeout padrão e diz que a árvore lida não é diferente da anterior.
 * Com o relógio solto, ela devolve a árvore nova.
 */
test('T09b: com o render parado, a seta já marcou a opção e a árvore ainda é a anterior', async ({
  page,
}) => {
  await page.clock.install();
  const { form, tree: antes } = await openWithProjectName(page);

  await page.clock.pauseAt(Date.now() + 1_000);
  await pickSecondOfFirstGroup(page, form);

  // O que a leitura antiga fazia: ler logo depois de `toBeChecked()`. A árvore
  // é a de antes da troca, e continua igual leitura após leitura.
  expect(await stableTree(page), 'estabilidade sozinha aceita a árvore velha').toEqual(antes);

  const recusa = await treeAfterChange(page, antes).then(
    () => null,
    (error: Error) => error,
  );
  expect(recusa, 'treeAfterChange devolveu a árvore anterior como se fosse a nova').not.toBeNull();
  expect(recusa!.message).toContain('diferente da anterior: NÃO');

  await page.clock.resume();
  const depois = await treeAfterChange(page, antes);
  expect(depois).not.toEqual(antes);
  expect(depois.length).toBeGreaterThan(20);
});

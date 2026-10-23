import { expect, test } from '@playwright/test';
import {
  PROJECT_NAME,
  PROJECT_NAME_KEY,
  TEMPLATES_ROUTE,
  chooseNonDefault,
  expectSelection,
  fieldBlock,
  holdNextGeneration,
  readCatalog,
  saveZip,
  waitForCatalog,
  watchGenerations,
} from './support';

/**
 * Os estados da geração pela tela, contra a API de verdade — critérios 2, 3 e
 * 4 de T09 (RF-05, RF-07 e RF-06).
 *
 * **O que `page.route` faz aqui, e o que não faz.** Ele nunca fabrica resposta.
 * Em dois casos ele só *segura* a requisição real até o teste afirmar o que a
 * tela mostra enquanto ela corre, e depois a solta com `route.continue()`; no
 * terceiro ele troca o nome do projeto no corpo para um que o servidor recusa,
 * porque a tela valida localmente e nunca mandaria esse nome. Nos três, quem
 * responde é o gerador .NET, e é a resposta dele que a tela trata.
 *
 * Nenhuma espera por tempo: cada passo espera um evento ou um estado.
 */

test('mostra "gerando" com o botão bloqueado entre o clique e o download (RF-05)', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  const form = await waitForCatalog(page);
  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);

  const { held } = await holdNextGeneration(page);
  const baixando = page.waitForEvent('download', { timeout: 120_000 });
  const botao = form.locator('button[type="submit"]');
  await botao.click();

  // A requisição saiu e está parada antes de chegar ao gerador: o que a tela
  // mostra agora é o estado "gerando", e nada mais o explica.
  const route = await held;
  await expect(botao).toBeDisabled();
  await expect(botao).toHaveText('Gerando…');
  await expect(form).toHaveAttribute('aria-busy', 'true');
  const indicador = form.locator('.actions__status[role="status"]');
  await expect(indicador).toBeVisible();
  await expect(indicador).toContainText('Montando o pacote');
  await expect(page.locator('.notice--done')).toHaveCount(0);

  await route.continue();
  const download = await baixando;
  await saveZip(download, testInfo);

  // E o estado sai quando a geração termina.
  await expect(botao).toBeEnabled();
  await expect(botao).toHaveText('Gerar projeto');
  await expect(form).not.toHaveAttribute('aria-busy', 'true');
  await expect(page.locator('.notice--done')).toBeVisible();
});

/**
 * RF-07, nas duas formas que uma pessoa realmente produz: duplo clique e Enter
 * repetido. A primeira requisição é segurada enquanto o resto das tentativas
 * acontece, para que todas caiam dentro da janela "gerando" — que é a janela
 * em que um segundo envio seria duplicado, e não uma geração nova.
 *
 * A contagem é da **rede** (`page.on('request')`) e dos **downloads** do
 * navegador, não do estado do componente.
 */
for (const forma of ['duplo clique', 'Enter repetido'] as const) {
  test(`envio duplicado por ${forma} gera um POST e um download só (RF-07)`, async ({
    page,
  }, testInfo) => {
    const contagem = watchGenerations(page);

    await page.goto('/');
    const form = await waitForCatalog(page);
    const nome = page.getByLabel('Nome do projeto');
    await nome.fill(PROJECT_NAME);

    const { held } = await holdNextGeneration(page);
    const baixando = page.waitForEvent('download', { timeout: 120_000 });
    const botao = form.locator('button[type="submit"]');

    if (forma === 'duplo clique') {
      await botao.dblclick();
    } else {
      await botao.focus();
      for (let i = 0; i < 4; i += 1) {
        await page.keyboard.press('Enter');
      }
    }

    const route = await held;

    // Ainda dentro da janela: mais Enter onde o foco estiver, e depois no campo
    // de texto, que é o envio implícito do formulário.
    await page.keyboard.press('Enter');
    await nome.focus();
    for (let i = 0; i < 3; i += 1) {
      await page.keyboard.press('Enter');
    }
    expect(contagem.posts(), 'POST /api/templates durante a geração').toBe(1);

    await route.continue();
    await saveZip(await baixando, testInfo);
    await expect(page.locator('.notice--done')).toBeVisible();

    // A geração terminou e nada mais foi digitado: qualquer segundo envio já
    // estaria na rede, e qualquer segundo download viria de um segundo POST.
    expect(contagem.posts(), 'POST /api/templates no total').toBe(1);
    expect(contagem.downloads(), 'downloads no total').toBe(1);
  });
}

/**
 * RF-06 contra o servidor. A tela valida o nome localmente, então o `400` real
 * só é alcançável trocando o corpo no caminho: a pessoa vê um nome válido, o
 * gerador recebe um que ele recusa, e o `ProblemDetails` que volta é o dele.
 */
test('a recusa do servidor aparece no campo certo e preserva todas as escolhas (RF-06)', async ({
  page,
}) => {
  const contagem = watchGenerations(page);
  await page.goto('/');
  const form = await waitForCatalog(page);
  const catalog = await readCatalog(page);

  const escolhas = await chooseNonDefault(page, catalog);
  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);

  // Um segmento que é palavra reservada do C#: válido para nenhum dos dois
  // lados, mas é o servidor quem o vê.
  const recusado = 'Acme.class.Api';
  await page.route(
    TEMPLATES_ROUTE,
    async (route) => {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      await route.continue({ postData: JSON.stringify({ ...body, [PROJECT_NAME_KEY]: recusado }) });
    },
    { times: 1 },
  );

  const respondeu = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/templates',
  );
  await form.locator('button[type="submit"]').click();
  const response = await respondeu;

  expect(response.status()).toBe(400);
  expect(response.headers()['content-type']).toContain('application/problem+json');
  const problem = (await response.json()) as { errors?: Record<string, string[]> };
  const errors = problem.errors ?? {};
  expect(Object.keys(errors), 'o servidor endereçou a recusa a algum campo').not.toHaveLength(0);

  // 1. Cada mensagem do servidor aparece inline, no campo que `errors` endereça.
  for (const [key, messages] of Object.entries(errors)) {
    const bloco = fieldBlock(page, key);
    await expect(bloco, `bloco do campo "${key}"`).toHaveCount(1);
    for (const message of messages) {
      await expect(bloco.locator('.field__error')).toContainText(message);
    }
  }
  expect(Object.keys(errors)).toContain(PROJECT_NAME_KEY);
  await expect(page.locator(`input[id="${PROJECT_NAME_KEY}"]`)).toHaveAttribute(
    'aria-invalid',
    'true',
  );
  await expect(page.locator('.notice--error')).toContainText('Nenhuma escolha foi perdida');

  // 2. Todas as escolhas não padrão continuam selecionadas, e o nome também.
  await expectSelection(page, escolhas);
  await expect(page.getByLabel('Nome do projeto')).toHaveValue(PROJECT_NAME);

  // 3. Nenhum download: a resposta foi um `400`, e ela já chegou.
  expect(contagem.posts()).toBe(1);
  expect(contagem.downloads()).toBe(0);
});

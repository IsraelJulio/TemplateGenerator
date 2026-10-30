import { expect, test } from '@playwright/test';
import type { TemplateOptionsCatalog } from '../src/app/core/catalog/template-options.model';
import {
  PROJECT_NAME,
  ZIP_SIGNATURE,
  chooseNonDefault,
  expectSelection,
  expectTreeMatchesZip,
  generateAndOpen,
  openWithProjectName,
  openZip,
  pickSecondOfFirstGroup,
  radioOf,
  readCatalog,
  saveZip,
  type Selection,
  treeAcross,
  waitForCatalog,
  zipEntries,
} from './support';

/**
 * Critério de aceite 11 de T03: **o download pela tela salva um ZIP de verdade
 * no navegador.**
 *
 * Nada aqui é dublado. A tela é servida pelo `ng serve`, o catálogo e o pacote
 * vêm da API .NET pelo proxy, e o arquivo inspecionado é o que o Chromium
 * gravou em disco ao clicar em "Gerar projeto". Se a API estiver fora do ar o
 * teste falha na subida, e é isso que se quer: um teste que passe com o gerador
 * desligado não prova o critério.
 *
 * **Nenhum valor de opção aparece neste arquivo.** A combinação dos primeiros
 * testes é a que o próprio catálogo traz como padrão — o formulário nasce
 * preenchido, e o teste só digita o nome do projeto; a do último é lida do
 * catálogo servido (T09). Isso não é economia de digitação: é a
 * mesma regra de RF-02 que vale para a tela, e mantém o teste válido se um
 * padrão mudar no backend.
 */

test('baixa um ZIP de verdade, e o pacote é o que a tela prometeu', async ({ page }, testInfo) => {
  // A "estrutura prevista" é lida **antes** de gerar: é a promessa que a tela
  // fez à pessoa, e é contra ela que o pacote será conferido. Este teste não
  // troca opção nenhuma, mas digitar o nome já muda a árvore, então ela vem
  // de `treeAcross`, como em qualquer troca (T09b).
  const { form, tree: prometido } = await openWithProjectName(page);

  await expect(page.getByRole('heading', { name: 'Gerador de projetos .NET' })).toBeVisible();

  expect(prometido.length).toBeGreaterThan(20);
  expect(prometido).toContain(`src/${PROJECT_NAME}/`);

  // A escuta é armada antes do clique: o download pode começar antes de
  // `click()` resolver, e aí o evento já teria passado.
  const baixando = page.waitForEvent('download', { timeout: 120_000 });
  await form.getByRole('button', { name: 'Gerar projeto' }).click();
  const download = await baixando;

  expect(download.suggestedFilename()).toBe(`${PROJECT_NAME}.zip`);

  // Fora do repositório: `test-results/` é descartável e ignorado pelo git.
  const saved = testInfo.outputPath(download.suggestedFilename());
  await download.saveAs(saved);

  const { readFileSync } = await import('node:fs');
  const bytes = new Uint8Array(readFileSync(saved));

  expect(bytes.length, 'o arquivo salvo está vazio').toBeGreaterThan(1024);
  expect([...bytes.slice(0, 4)], 'o arquivo salvo não começa com a assinatura de um ZIP').toEqual(
    ZIP_SIGNATURE,
  );

  // Abrir o pacote de verdade: um arquivo com a assinatura certa e corpo
  // corrompido passaria em qualquer checagem de cabeçalho.
  const files = openZip(bytes);
  const real = zipEntries(files);

  expect(real).toContain(`src/${PROJECT_NAME}/${PROJECT_NAME}.csproj`);
  expect(real).toContain('README.md');
  expect(real).toContain('.templategenerator/manifest.json');
  expect(real.some((path) => path.includes('.Api.Api'))).toBe(false);

  // Conteúdo, não só nomes de arquivo: é o que separa "o navegador salvou um
  // ZIP" de "o ZIP é a resposta do gerador a **esta** requisição". O manifesto
  // carrega as opções que a tela enviou (RF-22), e o `.csproj` carrega o nome
  // aplicado ao token do template.
  const manifest = JSON.parse(new TextDecoder().decode(files['.templategenerator/manifest.json']));
  expect(manifest.options.projectName).toBe(PROJECT_NAME);
  expect(manifest.templateVersion).toBeTruthy();

  const csproj = new TextDecoder().decode(files[`src/${PROJECT_NAME}/${PROJECT_NAME}.csproj`]);
  expect(csproj).toContain('<Project Sdk=');
  expect(csproj).not.toContain('__ProjectName__');

  expectTreeMatchesZip(prometido, real);

  // Estado 7 de `visual-spec.md`, agora sem dublê nenhum por trás.
  const concluido = page.locator('.notice--done');
  await expect(concluido).toBeVisible();
  await expect(concluido).toContainText(`${PROJECT_NAME}.zip`);
});

test('gera sem mouse: foco visível, escolha por setas e envio pelo teclado', async ({ page }) => {
  await page.goto('/');
  await waitForCatalog(page);

  const nome = page.getByLabel('Nome do projeto');
  await nome.focus();
  await expect(nome).toBeFocused();
  await page.keyboard.type(PROJECT_NAME);

  // Tabula até o primeiro grupo de rádio e anda dentro dele com as setas, que é
  // o comportamento que o atributo `name` do template existe para garantir.
  await page.keyboard.press('Tab');
  const primeiroRadio = page.locator('form.config input[type="radio"]').first();
  await expect(primeiroRadio).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowUp');
  await expect(primeiroRadio).toBeChecked();

  // O indicador de foco é um só na tela (visual-spec): 2 px em `--accent`.
  const outline = await primeiroRadio.evaluate((el) => {
    const style = getComputedStyle(el);
    return { width: style.outlineWidth, style: style.outlineStyle };
  });
  expect(outline.style).not.toBe('none');
  expect(Number.parseFloat(outline.width)).toBeGreaterThanOrEqual(2);

  const botao = page.getByRole('button', { name: 'Gerar projeto' });
  await botao.focus();
  await expect(botao).toBeFocused();

  const baixando = page.waitForEvent('download', { timeout: 120_000 });
  await page.keyboard.press('Enter');
  const download = await baixando;

  expect(download.suggestedFilename()).toBe(`${PROJECT_NAME}.zip`);
});

/**
 * A **segunda** opção do primeiro grupo de rádio, escolhida pela seta do
 * teclado. Nenhum valor é nomeado, pela mesma regra do cabeçalho deste arquivo:
 * o que se afirma é "a outra arquitetura que o catálogo oferece", seja ela qual
 * for.
 *
 * É o teste em que a corrida de T09b apareceu: a árvore era lida logo depois do
 * `toBeChecked()`, que passa antes do render. Agora a troca acontece dentro de
 * `treeAcross`, que lê a árvore antes dela e espera a nova assentar. É também o
 * sentido 2 da prova de T09b; o sentido 1, o mesmo cenário com a leitura
 * obsoleta, está em `tree-read.e2e.ts`.
 */
test('a outra arquitetura do catálogo também baixa o que a tela prometeu', async ({
  page,
}, testInfo) => {
  const { form, tree: antes } = await openWithProjectName(page);

  const prometido = await treeAcross(page, () => pickSecondOfFirstGroup(page, form));
  // A árvore de antes é a mesma que o sentido 1 usa como leitura obsoleta.
  expect(prometido).not.toEqual(antes);
  expect(prometido.length).toBeGreaterThan(20);

  const real = await generateAndOpen(page, form, testInfo);

  expectTreeMatchesZip(prometido, real);
});

/**
 * ADR-0012 pela tela, **com a API dizendo o que está sem template** (T10,
 * ressalva 5 de T09). Hoje o catálogo real traz `unavailable: []`, então o
 * teste intercepta `GET /api/template-options`, busca a resposta de verdade e
 * só acrescenta uma entrada em `unavailable` — o resto do catálogo é o do
 * servidor. A entrada é tirada do próprio catálogo: o segundo valor do primeiro
 * campo de escolha (nunca o padrão), com uma razão que o teste inventa para
 * poder procurá-la na tela.
 *
 * O que se afirma: o valor marcado chega desabilitado, com a razão visível e
 * ligada ao controle por `aria-describedby`; o resto do grupo segue clicável.
 */
test('desabilita na tela o que a API marcou como sem template', async ({ page }) => {
  const razao = 'Ainda não existe template para esta opção (marcação do teste).';
  let marcado: { field: string; value: string } | undefined;

  await page.route('**/api/template-options', async (route) => {
    const resposta = await route.fetch();
    const catalogo = (await resposta.json()) as TemplateOptionsCatalog;
    const [campo, definicao] = Object.entries(catalogo.fields).find(
      ([, f]) => f.type === 'choice' && (f.values ?? []).length > 1,
    )!;
    const alvo = (definicao.values ?? []).find((v) => v.value !== definicao.default)!;
    marcado = { field: campo, value: String(alvo.value) };
    await route.fulfill({
      response: resposta,
      json: { ...catalogo, unavailable: [{ field: campo, value: alvo.value, reason: razao }] },
    });
  });

  await page.goto('/');
  const form = await waitForCatalog(page);
  expect(marcado, 'a tela não pediu o catálogo pela rota interceptada').toBeDefined();

  const radio = radioOf(form, marcado!.field, marcado!.value);
  await expect(radio).toBeDisabled();
  const id = await radio.getAttribute('aria-describedby');
  expect(id, 'opção sem template sem razão associada').not.toBeNull();
  const motivo = page.locator(`[id="${id}"]`);
  await expect(motivo).toBeVisible();
  await expect(motivo).toContainText(razao);

  // O valor padrão do mesmo campo continua selecionado e clicável, e nenhum
  // outro valor foi desabilitado por esta marcação.
  const mesmoGrupo = form.locator(`input[type="radio"][data-field="${marcado!.field}"]`);
  const habilitados = await mesmoGrupo.evaluateAll(
    (els) => els.filter((el) => !(el as HTMLInputElement).disabled).length,
  );
  expect(habilitados).toBe((await mesmoGrupo.count()) - 1);
  await expect(
    form.locator(`input[type="radio"][data-field="${marcado!.field}"]:checked`),
  ).toBeEnabled();
});

/**
 * O outro motivo de desabilitar, com a API de verdade e sem interceptar nada: a
 * **restrição** do catálogo. A tela nasce no padrão e, em cada campo, o valor
 * que a restrição proíbe naquele momento chega desabilitado com a razão
 * visível e ligada ao controle; nenhum valor marcado por padrão está
 * desabilitado, senão a tela nasceria impossível de enviar.
 *
 * (Antes de T10 este teste se chamava "...o que a API marcou como sem template"
 * e passava só por isto, porque o catálogo real traz `unavailable: []`.)
 */
test('desabilita na tela o que a restrição do catálogo proíbe, com a razão visível', async ({
  page,
}) => {
  await page.goto('/');
  const form = await waitForCatalog(page);

  const radios = form.locator('input[type="radio"]');
  const total = await radios.count();
  expect(total).toBeGreaterThan(0);

  let desabilitados = 0;

  for (let i = 0; i < total; i += 1) {
    const radio = radios.nth(i);
    if (!(await radio.isDisabled())) {
      continue;
    }
    desabilitados += 1;

    const id = await radio.getAttribute('aria-describedby');
    expect(id, 'opção desabilitada sem razão associada').not.toBeNull();
    await expect(page.locator(`[id="${id}"]`)).toBeVisible();
  }

  for (let i = 0; i < total; i += 1) {
    const radio = radios.nth(i);
    if (await radio.isChecked()) {
      expect(await radio.isDisabled()).toBe(false);
    }
  }

  expect(desabilitados, 'nenhuma opção veio desabilitada — o catálogo mudou?').toBeGreaterThan(0);
});

/**
 * Critério 5 de T09: uma combinação **não padrão**, preenchida pela tela com
 * valores lidos do catálogo — a última opção habilitada de cada campo de
 * escolha e o interruptor invertido (`chooseNonDefault`). Nenhum valor é
 * nomeado aqui.
 *
 * O que se afirma é o **manifesto** do ZIP (RF-22) contra as escolhas da tela, e
 * um conjunto de caminhos que vale em qualquer combinação. A comparação
 * árvore × ZIP está nos testes acima e em `zip-structure.spec.ts`; quem
 * quiser fazê-la depois de mudar opções lê a árvore com `treeAcross` (T09b).
 */
test('uma combinação não padrão escolhida pela tela chega inteira ao manifesto do ZIP', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  const form = await waitForCatalog(page);
  const catalog = await readCatalog(page);

  // A árvore da tela é lida antes das trocas e depois de assentada (T09b): são
  // várias trocas seguidas (uma por campo e o nome), e `treeAcross` espera a
  // árvore nova ficar igual em duas leituras, não o primeiro estado intermediário.
  let escolhas: Selection = {};
  const prometido = await treeAcross(page, async () => {
    escolhas = await chooseNonDefault(page, catalog);
    await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);
  });

  // Não padrão de verdade: todo campo que oferece mais de uma posição saiu do
  // padrão. Um catálogo em que só sobrasse o padrão faria este teste repetir o
  // primeiro.
  const mudou = Object.entries(catalog.fields).filter(
    ([key, field]) => escolhas[key] !== field.default,
  );
  const mutaveis = Object.values(catalog.fields).filter(
    (field) => field.type === 'boolean' || (field.values ?? []).length > 1,
  );
  expect(mudou.length, 'campos fora do padrão').toBe(mutaveis.length);
  expect(mudou.length).toBeGreaterThan(1);

  const baixando = page.waitForEvent('download', { timeout: 120_000 });
  await form.getByRole('button', { name: 'Gerar projeto' }).click();
  const download = await baixando;
  expect(download.suggestedFilename()).toBe(`${PROJECT_NAME}.zip`);

  const files = openZip(await saveZip(download, testInfo));
  const real = zipEntries(files);

  // O que a tela prometeu para esta combinação é o que o ZIP trouxe.
  expect(prometido.length).toBeGreaterThan(20);
  expectTreeMatchesZip(prometido, real);

  const manifest = JSON.parse(new TextDecoder().decode(files['.templategenerator/manifest.json']));
  expect(manifest).toEqual({
    templateVersion: catalog.templateVersion,
    options: { projectName: PROJECT_NAME, ...escolhas },
  });

  // Caminhos-chave que valem em qualquer combinação (RF-10).
  for (const path of ['README.md', 'requests.http', `${PROJECT_NAME}.sln`]) {
    expect(real, path).toContain(path);
  }
  const csprojs = real.filter((path) => path.endsWith('.csproj'));
  expect(csprojs.some((path) => path.startsWith('src/'))).toBe(true);
  expect(csprojs.some((path) => path.startsWith('tests/'))).toBe(true);
  for (const path of csprojs) {
    expect(path.split('/').at(-1), path).toMatch(
      new RegExp(`^${PROJECT_NAME.replace(/\./g, '\\.')}`),
    );
  }
  expect(real.filter((path) => path.includes('__ProjectName__'))).toEqual([]);

  const readme = new TextDecoder().decode(files['README.md']);
  expect(readme).toContain(PROJECT_NAME);

  // E a tela, depois do download, continua mostrando a mesma combinação.
  await expectSelection(page, escolhas);
  await expect(page.locator('.notice--done')).toContainText(`${PROJECT_NAME}.zip`);
});

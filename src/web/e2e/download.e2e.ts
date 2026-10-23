import { expect, test } from '@playwright/test';
import {
  PROJECT_NAME,
  ZIP_SIGNATURE,
  chooseNonDefault,
  expectSelection,
  openZip,
  readCatalog,
  saveZip,
  treeOnScreen,
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
  await page.goto('/');
  const form = await waitForCatalog(page);

  await expect(page.getByRole('heading', { name: 'Gerador de projetos .NET' })).toBeVisible();

  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);

  // A "estrutura prevista" é lida **antes** de gerar: é a promessa que a tela
  // fez à pessoa, e é contra ela que o pacote será conferido.
  const prometido = await treeOnScreen(page);
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

  const sobrando = prometido.filter((path) => !real.includes(path));
  const faltando = real.filter((path) => !prometido.includes(path));
  expect(
    { sobrando, faltando },
    'A árvore exibida na tela divergiu do ZIP que o navegador acabou de baixar.',
  ).toEqual({ sobrando: [], faltando: [] });

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
 */
test('a outra arquitetura do catálogo também baixa o que a tela prometeu', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  const form = await waitForCatalog(page);

  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);

  const primeiroGrupo = form.locator('fieldset').first();
  await primeiroGrupo.locator('input[type="radio"]').first().focus();
  await page.keyboard.press('ArrowDown');
  await expect(primeiroGrupo.locator('input[type="radio"]').nth(1)).toBeChecked();

  const prometido = await treeOnScreen(page);
  expect(prometido.length).toBeGreaterThan(20);

  const baixando = page.waitForEvent('download', { timeout: 120_000 });
  await form.getByRole('button', { name: 'Gerar projeto' }).click();
  const download = await baixando;

  const saved = testInfo.outputPath(download.suggestedFilename());
  await download.saveAs(saved);

  const { readFileSync } = await import('node:fs');
  const real = zipEntries(openZip(new Uint8Array(readFileSync(saved))));

  const sobrando = prometido.filter((path) => !real.includes(path));
  const faltando = real.filter((path) => !prometido.includes(path));
  expect(
    { sobrando, faltando },
    'A árvore exibida na tela divergiu do ZIP que o navegador acabou de baixar.',
  ).toEqual({ sobrando: [], faltando: [] });
});

/**
 * ADR-0012 pela tela: o valor que ainda não gera projeto chega **desabilitado**,
 * com a razão que o catálogo mandou, e o que tem template continua clicável.
 *
 * Sem nomear nenhum dos dois: o teste conta o que a API de verdade marcou, e
 * continua valendo no dia em que a lista encolher — quando ela esvaziar, é o
 * `else` que passa a ser exercitado.
 */
test('desabilita na tela o que a API marcou como sem template', async ({ page }) => {
  const form = await (async () => {
    await page.goto('/');
    return waitForCatalog(page);
  })();

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

    // A razão é visível e está ligada ao controle por `aria-describedby`, que é
    // o que um leitor de tela anuncia junto com "indisponível".
    const id = await radio.getAttribute('aria-describedby');
    expect(id, 'opção desabilitada sem razão associada').not.toBeNull();
    await expect(page.locator(`[id="${id}"]`)).toBeVisible();
  }

  // Nenhum valor selecionado por padrão pode estar desabilitado: seria uma tela
  // que nasce impossível de enviar.
  for (let i = 0; i < total; i += 1) {
    const radio = radios.nth(i);
    if (await radio.isChecked()) {
      expect(await radio.isDisabled()).toBe(false);
    }
  }

  expect(desabilitados, 'nenhuma opção veio desabilitada — a API mudou?').toBeGreaterThan(0);
});

/**
 * Critério 5 de T09: uma combinação **não padrão**, preenchida pela tela com
 * valores lidos do catálogo — a última opção habilitada de cada campo de
 * escolha e o interruptor invertido (`chooseNonDefault`). Nenhum valor é
 * nomeado aqui.
 *
 * O que se afirma é o **manifesto** do ZIP (RF-22) contra as escolhas da tela, e
 * um conjunto de caminhos que vale em qualquer combinação. A árvore da tela não
 * é lida depois de mudar opções: essa leitura tem uma corrida conhecida, que é
 * a T09b, e a comparação árvore × ZIP já está nos testes acima e em
 * `zip-structure.spec.ts`.
 */
test('uma combinação não padrão escolhida pela tela chega inteira ao manifesto do ZIP', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  const form = await waitForCatalog(page);
  const catalog = await readCatalog(page);

  const escolhas = await chooseNonDefault(page, catalog);
  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);

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

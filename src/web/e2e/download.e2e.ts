import { Locator, Page, expect, test } from '@playwright/test';
import { unzipSync } from 'fflate';

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
 * **Nenhum valor de opção aparece neste arquivo.** A combinação exercitada é a
 * que o próprio catálogo traz como padrão — o formulário nasce preenchido, e o
 * teste só digita o nome do projeto. Isso não é economia de digitação: é a
 * mesma regra de RF-02 que vale para a tela, e mantém o teste válido se um
 * padrão mudar no backend.
 */

/** O nome que expõe a decisão de `generated-projects.md` sobre o sufixo. */
const PROJECT_NAME = 'Acme.Billing.Api';

/** Um ZIP começa com a assinatura local do primeiro registro. */
const ZIP_SIGNATURE = [0x50, 0x4b, 0x03, 0x04];

/**
 * A árvore que a tela está mostrando, como caminhos completos.
 *
 * O componente renderiza uma linha por nó, com a profundidade na custom
 * property `--depth` e o nome já com a `/` final quando é diretório. Remontar o
 * caminho a partir disso lê exatamente o que a pessoa vê, que é o ponto — ler
 * `projectStructure()` de novo seria conferir a derivação contra ela mesma.
 */
async function treeOnScreen(page: Page): Promise<string[]> {
  const lines = await page.locator('.tree .tree__line').all();
  const stack: string[] = [];
  const paths: string[] = [];

  for (const line of lines) {
    const depth = Number(
      await line.evaluate((el) => getComputedStyle(el).getPropertyValue('--depth')),
    );
    const name = (await line.locator('.tree__name').innerText()).trim();

    stack.length = depth;
    stack[depth] = name;
    paths.push(stack.join(''));
  }

  return paths;
}

/** O pacote aberto de verdade: bytes descomprimidos, por caminho. */
function openZip(bytes: Uint8Array): Record<string, Uint8Array> {
  return unzipSync(bytes);
}

/** As entradas do ZIP, no mesmo formato da árvore: diretório termina em `/`. */
function zipEntries(files: Record<string, Uint8Array>): string[] {
  const entries = new Set<string>(Object.keys(files));

  // Um ZIP não precisa carregar registro próprio para diretório, e o do gerador
  // não carrega. A árvore da tela mostra pasta, então as pastas implícitas
  // entram aqui — derivadas dos arquivos, nunca supostas.
  for (const path of Object.keys(files)) {
    const segments = path.split('/');
    for (let i = 1; i < segments.length; i += 1) {
      entries.add(`${segments.slice(0, i).join('/')}/`);
    }
  }

  return [...entries].sort();
}

async function waitForCatalog(page: Page): Promise<Locator> {
  const form = page.locator('form.config');
  // O catálogo vem da API de verdade; a tela fica em "Carregando as opções…"
  // até ele chegar.
  await expect(form).toBeVisible({ timeout: 30_000 });
  return form;
}

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

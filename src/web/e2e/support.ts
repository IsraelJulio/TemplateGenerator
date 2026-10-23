import { Download, Locator, Page, Route, TestInfo, expect } from '@playwright/test';
import { unzipSync } from 'fflate';
import type {
  OptionValue,
  TemplateOptionsCatalog,
} from '../src/app/core/catalog/template-options.model';

/**
 * Apoio comum dos fluxos ponta a ponta. Não é teste: `playwright.config.ts`
 * só coleta `*.e2e.ts`.
 *
 * **Nenhum valor de opção aparece aqui**, pela mesma regra de RF-02 que vale
 * para a tela: o que o teste escolhe é lido do catálogo que a API de verdade
 * serve, e continua valendo se um valor ou um padrão mudar no backend.
 */

/** O nome que expõe a decisão de `generated-projects.md` sobre o sufixo. */
export const PROJECT_NAME = 'Acme.Billing.Api';

/** A chave do nome do projeto no corpo do `POST` (fora de `fields`, por contrato). */
export const PROJECT_NAME_KEY = 'projectName';

/** Um ZIP começa com a assinatura local do primeiro registro. */
export const ZIP_SIGNATURE = [0x50, 0x4b, 0x03, 0x04];

/** A rota do gerador, como o navegador a chama (pelo proxy do `ng serve`). */
export const TEMPLATES_ROUTE = '**/api/templates';

export type Selection = Record<string, OptionValue>;

/**
 * **Uma** leitura da árvore que a tela está mostrando, como caminhos completos.
 *
 * O componente renderiza uma linha por nó, com a profundidade na custom
 * property `--depth` e o nome já com a `/` final quando é diretório. Remontar o
 * caminho a partir disso lê exatamente o que a pessoa vê, que é o ponto — ler
 * `projectStructure()` de novo seria conferir a derivação contra ela mesma.
 *
 * A leitura inteira acontece num só `evaluateAll`, isto é, numa só tarefa do
 * navegador: o render do Angular não tem como se intercalar no meio dela e
 * misturar linhas de duas árvores.
 *
 * **Não é exportada, de propósito (T09b).** Uma leitura só não sabe se o render
 * que a última troca agendou já aconteceu. Sem zone.js, o Angular agenda o
 * render para depois do evento, enquanto o `checked` do rádio o navegador põe na
 * hora — `toBeChecked()` passa com a árvore ainda velha na tela. Quem lê a árvore
 * usa {@link stableTree} ou {@link treeAfterChange}.
 */
async function readTree(page: Page): Promise<string[]> {
  return page.locator('.tree .tree__line').evaluateAll((lines) => {
    const stack: string[] = [];
    return lines.map((line) => {
      const depth = Number(getComputedStyle(line).getPropertyValue('--depth'));
      const name = (line.querySelector<HTMLElement>('.tree__name')?.innerText ?? '').trim();
      stack.length = depth;
      stack[depth] = name;
      return stack.join('');
    });
  });
}

function sameTree(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && a.every((path, i) => path === b[i]);
}

/** O que a falha mostra: cada condição, cumprida ou não, e a árvore lida. */
function describeRead(conditions: Record<string, boolean>, tree: readonly string[]): string {
  return [
    ...Object.entries(conditions).map(([name, met]) => `${name}: ${met ? 'sim' : 'NÃO'}`),
    `árvore lida (${tree.length} linhas):`,
    ...tree.map((path) => `  ${path}`),
  ].join('\n');
}

const SETTLED = 'assentou';

/**
 * Lê a árvore até que ela cumpra `required` **e** seja igual à leitura anterior.
 * Usa o timeout padrão do `expect`, sem `waitForTimeout`: o intervalo entre as
 * leituras é o de `expect.poll`. A falha diz qual condição não se cumpriu e
 * mostra a árvore lida.
 */
async function settle(
  page: Page,
  message: string,
  required: (current: readonly string[]) => Record<string, boolean>,
): Promise<string[]> {
  let last: string[] | undefined;
  await expect
    .poll(
      async () => {
        const current = await readTree(page);
        const conditions = {
          ...required(current),
          'igual em duas leituras seguidas': last !== undefined && sameTree(current, last),
        };
        last = current;
        return Object.values(conditions).every(Boolean)
          ? SETTLED
          : describeRead(conditions, current);
      },
      { message },
    )
    .toBe(SETTLED);
  return last!;
}

/**
 * A árvore da tela **quando nada mudou desde o carregamento**: não vazia e igual
 * em duas leituras seguidas.
 *
 * Serve de ponto de partida para {@link treeAfterChange}, e só para isso.
 * Estabilidade sozinha é satisfeita por uma árvore que ainda não mudou: logo
 * depois de uma troca, esta função devolve a árvore **anterior** sem reclamar. A
 * prova está em `tree-read.e2e.ts`.
 */
export function stableTree(page: Page): Promise<string[]> {
  return settle(page, 'a árvore da tela não assentou', (current) => ({
    'não vazia': current.length > 0,
  }));
}

/**
 * A árvore da tela **depois de uma troca**: a primeira leitura que é diferente de
 * `previous` **e** igual à leitura seguinte (T09b, critério 4).
 *
 * As duas condições são necessárias. Estabilidade sozinha é satisfeita pela
 * árvore que ainda não mudou. Diferença sozinha pode pegar um estado
 * intermediário de render. `previous` tem de ser lido **antes** da troca, com
 * {@link stableTree} ou com uma chamada anterior desta função.
 *
 * Se a árvore não muda, a chamada falha no timeout padrão dizendo qual condição
 * faltou, em vez de devolver a árvore anterior como se fosse a nova.
 */
export function treeAfterChange(page: Page, previous: readonly string[]): Promise<string[]> {
  return settle(
    page,
    'a árvore da tela não assentou numa leitura nova depois da troca',
    (current) => ({
      'diferente da anterior': !sameTree(current, previous),
    }),
  );
}

/** A comparação árvore × ZIP: o que a tela prometeu contra o que o pacote trouxe. */
export function expectTreeMatchesZip(promised: readonly string[], real: readonly string[]): void {
  const sobrando = promised.filter((path) => !real.includes(path));
  const faltando = real.filter((path) => !promised.includes(path));
  expect(
    { sobrando, faltando },
    'A árvore exibida na tela divergiu do ZIP que o navegador acabou de baixar.',
  ).toEqual({ sobrando: [], faltando: [] });
}

/** O pacote aberto de verdade: bytes descomprimidos, por caminho. */
export function openZip(bytes: Uint8Array): Record<string, Uint8Array> {
  return unzipSync(bytes);
}

/** As entradas do ZIP, no mesmo formato da árvore: diretório termina em `/`. */
export function zipEntries(files: Record<string, Uint8Array>): string[] {
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

/** Salva o download fora do repositório e devolve os bytes, já conferidos como ZIP. */
export async function saveZip(download: Download, testInfo: TestInfo): Promise<Uint8Array> {
  // `test-results/` é descartável e ignorado pelo git.
  const saved = testInfo.outputPath(download.suggestedFilename());
  await download.saveAs(saved);

  const { readFileSync } = await import('node:fs');
  const bytes = new Uint8Array(readFileSync(saved));

  expect(bytes.length, 'o arquivo salvo está vazio').toBeGreaterThan(1024);
  expect([...bytes.slice(0, 4)], 'o arquivo salvo não começa com a assinatura de um ZIP').toEqual(
    ZIP_SIGNATURE,
  );
  return bytes;
}

export async function waitForCatalog(page: Page): Promise<Locator> {
  const form = page.locator('form.config');
  // O catálogo vem da API de verdade; a tela fica em "Carregando as opções…"
  // até ele chegar.
  await expect(form).toBeVisible({ timeout: 30_000 });
  return form;
}

/**
 * Abre a tela, espera o catálogo e digita o nome do projeto. Devolve o
 * formulário e a árvore já com o nome.
 *
 * Digitar o nome também é uma troca: com o campo vazio a árvore mostra um
 * marcador no lugar do nome, e o nome digitado só entra nos caminhos no render
 * seguinte. Por isso a árvore devolvida é lida com {@link treeAfterChange}, a
 * partir da árvore do campo vazio.
 */
export async function openWithProjectName(page: Page): Promise<{ form: Locator; tree: string[] }> {
  await page.goto('/');
  const form = await waitForCatalog(page);
  const emptyName = await stableTree(page);
  await page.getByLabel('Nome do projeto').fill(PROJECT_NAME);
  const tree = await treeAfterChange(page, emptyName);
  return { form, tree };
}

/**
 * Escolhe pela seta do teclado a **segunda** opção do primeiro grupo de rádio:
 * "a outra arquitetura que o catálogo oferece", seja ela qual for.
 *
 * O `toBeChecked()` confirma a escolha, **não** o render. O navegador marca o
 * rádio na hora da seta, e a árvore só muda quando o render agendado acontecer.
 * Quem quiser a árvore nova chama {@link treeAfterChange} com a árvore lida antes.
 */
export async function pickSecondOfFirstGroup(page: Page, form: Locator): Promise<void> {
  const radios = form.locator('fieldset').first().locator('input[type="radio"]');
  await radios.first().focus();
  await page.keyboard.press('ArrowDown');
  await expect(radios.nth(1)).toBeChecked();
}

/** Clica em "Gerar projeto", salva o download e devolve as entradas do ZIP. */
export async function generateAndOpen(
  page: Page,
  form: Locator,
  testInfo: TestInfo,
): Promise<string[]> {
  // A escuta é armada antes do clique: o download pode começar antes de
  // `click()` resolver, e aí o evento já teria passado.
  const downloading = page.waitForEvent('download', { timeout: 120_000 });
  await form.getByRole('button', { name: 'Gerar projeto' }).click();
  return zipEntries(openZip(await saveZip(await downloading, testInfo)));
}

/**
 * O catálogo que a API de verdade serve agora, pelo mesmo proxy que a tela
 * usa. É dele que os testes tiram campos, valores e padrões.
 */
export async function readCatalog(page: Page): Promise<TemplateOptionsCatalog> {
  const response = await page.request.get('/api/template-options');
  expect(response.status(), 'GET /api/template-options').toBe(200);
  return (await response.json()) as TemplateOptionsCatalog;
}

/** O controle de um valor de um campo do catálogo, pelas marcas que o template põe. */
export function radioOf(form: Locator, field: string, value: string): Locator {
  return form.locator(`input[type="radio"][data-field="${field}"][data-value="${value}"]`);
}

export function toggleOf(form: Locator, field: string): Locator {
  return form.locator(`input[type="checkbox"][data-field="${field}"]`);
}

/**
 * O bloco de campo para onde a tela endereça uma mensagem com esta chave: o do
 * nome do projeto ou o de um campo do catálogo.
 */
export function fieldBlock(page: Page, key: string): Locator {
  const control =
    key === PROJECT_NAME_KEY
      ? page.locator(`input[id="${PROJECT_NAME_KEY}"]`)
      : page.locator(`[data-field="${key}"]`);
  return page.locator('form.config .field').filter({ has: control });
}

/**
 * Preenche pela tela uma combinação **não padrão**, com valores lidos do
 * catálogo: em cada campo de escolha, na ordem do catálogo, a última opção que a
 * tela oferece habilitada naquele momento — portanto disponível e compatível
 * com o que já foi escolhido —; em cada interruptor, a posição oposta à padrão.
 *
 * Devolve a seleção que ficou na tela, conferida controle a controle.
 */
export async function chooseNonDefault(
  page: Page,
  catalog: TemplateOptionsCatalog,
): Promise<Selection> {
  const form = page.locator('form.config');
  const chosen: Selection = {};

  for (const [key, field] of Object.entries(catalog.fields)) {
    if (field.type === 'boolean') {
      const toggle = toggleOf(form, key);
      await toggle.setChecked(!field.default);
      chosen[key] = !field.default;
      continue;
    }

    const values = [...(field.values ?? [])].reverse();
    let picked: string | null = null;
    for (const option of values) {
      const radio = radioOf(form, key, option.value);
      if (await radio.isEnabled()) {
        await radio.check();
        picked = option.value;
        break;
      }
    }
    expect(picked, `nenhuma opção habilitada em "${key}"`).not.toBeNull();
    chosen[key] = picked!;
  }

  // A seleção final, relida da tela: uma escolha posterior não pode ter
  // desfeito uma anterior.
  await expectSelection(page, chosen);
  return chosen;
}

/** Afirma que cada controle mostra o valor desta seleção. */
export async function expectSelection(page: Page, selection: Selection): Promise<void> {
  const form = page.locator('form.config');
  for (const [key, value] of Object.entries(selection)) {
    if (typeof value === 'boolean') {
      if (value) {
        await expect(toggleOf(form, key), key).toBeChecked();
      } else {
        await expect(toggleOf(form, key), key).not.toBeChecked();
      }
    } else {
      await expect(radioOf(form, key, value), `${key}=${value}`).toBeChecked();
    }
  }
}

/**
 * Segura a **próxima** chamada a `POST /api/templates` até o teste soltá-la.
 *
 * Não fabrica nada: quando o teste chama `route.continue()`, a requisição real
 * segue para o gerador e a resposta é a dele. Serve para afirmar o que a tela
 * mostra **enquanto** a geração corre, sem `waitForTimeout`.
 */
export async function holdNextGeneration(page: Page): Promise<{ held: Promise<Route> }> {
  let resolve!: (route: Route) => void;
  const held = new Promise<Route>((done) => (resolve = done));
  await page.route(TEMPLATES_ROUTE, (route) => resolve(route), { times: 1 });
  // Embrulhado num objeto de propósito: devolver a promessa direto de uma
  // função `async` a achataria, e o `await` da chamada esperaria a requisição
  // que o próprio teste ainda não disparou.
  return { held };
}

/** Conta, na rede, as chamadas a `POST /api/templates` e os downloads da página. */
export function watchGenerations(page: Page): { posts: () => number; downloads: () => number } {
  let posts = 0;
  let downloads = 0;
  page.on('request', (request) => {
    if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/templates') {
      posts += 1;
    }
  });
  page.on('download', () => {
    downloads += 1;
  });
  return { posts: () => posts, downloads: () => downloads };
}

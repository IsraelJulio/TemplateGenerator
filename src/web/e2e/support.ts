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
 * A árvore que a tela está mostrando, como caminhos completos.
 *
 * O componente renderiza uma linha por nó, com a profundidade na custom
 * property `--depth` e o nome já com a `/` final quando é diretório. Remontar o
 * caminho a partir disso lê exatamente o que a pessoa vê, que é o ponto — ler
 * `projectStructure()` de novo seria conferir a derivação contra ela mesma.
 *
 * **Só para a combinação que a tela já mostra estável.** Ler a árvore logo
 * depois de mudar uma opção tem uma corrida conhecida (T09b); os testes novos de
 * T09 afirmam o manifesto e caminhos-chave do ZIP em vez de chamar isto.
 */
export async function treeOnScreen(page: Page): Promise<string[]> {
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

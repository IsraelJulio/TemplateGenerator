import { Page, expect, test } from '@playwright/test';
import { chooseNonDefault, readCatalog, waitForCatalog } from './support';

/**
 * Critério 6 de T09: **a página nunca rola na horizontal** (`visual-spec.md`,
 * "Layout"), comprovado por medição — e não escondido por um
 * `overflow-x: hidden` no body, que saiu de `styles.css` nesta tarefa.
 *
 * Sete larguras × três estados de conteúdo = 21 medições por projeto. As
 * larguras são as bordas que o CSS de fato tem: 320 (o menor celular), 416
 * (26rem, onde o resumo troca de grade), 768, 960 e 961 (os dois lados de
 * 60rem, onde o layout vira duas colunas e o gutter dobra), 1024 e 1440.
 *
 * Cada medição confere `scrollWidth <= clientWidth` no documento **e** no
 * body, e roda uma sonda elemento a elemento que nomeia o culpado: um número
 * sozinho diz que transbordou, não onde.
 */

const WIDTHS = [320, 416, 768, 960, 961, 1024, 1440] as const;

/** 64 letras, sem ponto: um único segmento, o pior caso para quebra de linha. */
const LONG_NAME = 'Abcdefghijklmnopqrstuvwxyz'.repeat(3).slice(0, 64);

interface Medicao {
  readonly doc: { scroll: number; client: number };
  readonly body: { scroll: number; client: number };
  readonly culpados: readonly string[];
}

/**
 * Mede dentro do navegador. Um elemento só é culpado se passa da borda direita
 * **e** nenhum ancestro o recorta: o que transborda dentro de um quadro com
 * `overflow-x` próprio (a árvore rola no próprio quadro, de propósito) não
 * empurra a página — e o quadro, se passasse da borda, seria ele o culpado.
 */
async function medir(page: Page): Promise<Medicao> {
  return page.evaluate(() => {
    const root = document.documentElement;
    const limit = root.clientWidth;

    const recortado = (el: Element): boolean => {
      for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) {
        if (getComputedStyle(p).overflowX !== 'visible') return true;
      }
      return false;
    };

    const nome = (el: Element): string => {
      const classes = [...el.classList].map((c) => `.${c}`).join('');
      const id = el.id ? `#${el.id}` : '';
      const texto = (el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40);
      const right = el.getBoundingClientRect().right.toFixed(1);
      return `<${el.tagName.toLowerCase()}${id}${classes}> right=${right} "${texto}"`;
    };

    const culpados = [...document.body.querySelectorAll('*')]
      .filter((el) => el.getBoundingClientRect().right > limit && !recortado(el))
      .map(nome);

    return {
      doc: { scroll: root.scrollWidth, client: root.clientWidth },
      body: { scroll: document.body.scrollWidth, client: document.body.clientWidth },
      culpados,
    };
  });
}

/** Espera o layout assentar depois de mudar a largura: fontes e dois quadros. */
async function assentar(page: Page): Promise<void> {
  await page.evaluate(async () => {
    await document.fonts.ready;
    await new Promise<void>((done) =>
      requestAnimationFrame(() => requestAnimationFrame(() => done())),
    );
  });
}

type Estado = 'inicial' | 'combinação não padrão' | 'nome de 64 letras';

async function prepararEstado(page: Page, estado: Estado): Promise<void> {
  await page.setViewportSize({ width: WIDTHS[0], height: 800 });
  await page.goto('/');
  await waitForCatalog(page);

  if (estado === 'combinação não padrão') {
    await chooseNonDefault(page, await readCatalog(page));
    await page.getByLabel('Nome do projeto').fill('Acme.Billing.Api');
  } else if (estado === 'nome de 64 letras') {
    expect(LONG_NAME).toHaveLength(64);
    const nome = page.getByLabel('Nome do projeto');
    await nome.fill(LONG_NAME);
    // Sai do campo, para a mensagem de validação (se houver) também entrar na
    // medida — e confere que o nome é aceito, senão o estado seria outro.
    await nome.blur();
    await expect(page.locator('.summary__name')).toHaveText(LONG_NAME);
  }
}

test('a página não rola na horizontal em sete larguras e três estados de conteúdo', async ({
  page,
}) => {
  const estados: readonly Estado[] = ['inicial', 'combinação não padrão', 'nome de 64 letras'];
  const falhas: string[] = [];
  let medicoes = 0;

  for (const estado of estados) {
    await prepararEstado(page, estado);

    for (const width of WIDTHS) {
      await page.setViewportSize({ width, height: 800 });
      await assentar(page);
      const m = await medir(page);
      medicoes += 1;

      // O viewport tem de ter mudado mesmo, senão a medida é de outra largura.
      expect(m.doc.client, `largura do documento em ${width}px`).toBeLessThanOrEqual(width);

      const problemas: string[] = [];
      if (m.doc.scroll > m.doc.client) {
        problemas.push(`documentElement.scrollWidth ${m.doc.scroll} > clientWidth ${m.doc.client}`);
      }
      if (m.body.scroll > m.body.client) {
        problemas.push(`body.scrollWidth ${m.body.scroll} > clientWidth ${m.body.client}`);
      }
      if (m.culpados.length > 0) {
        problemas.push(`culpados:\n      ${m.culpados.slice(0, 8).join('\n      ')}`);
      }
      if (problemas.length > 0) {
        falhas.push(`[${estado} @ ${width}px]\n    ${problemas.join('\n    ')}`);
      }
    }
  }

  expect(medicoes).toBe(WIDTHS.length * estados.length);
  expect(falhas, `transbordo horizontal:\n${falhas.join('\n')}`).toEqual([]);
});

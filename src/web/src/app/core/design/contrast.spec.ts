import { readFileSync } from 'node:fs';
import { contrastRatio, readColorTokens } from './contrast';

/**
 * O critério de aceite de T02 pede contraste AA "para texto e para o indicador
 * de foco". Este teste lê os tokens do `src/styles.css` de verdade e recalcula
 * cada par que a tela realmente usa — trocar um token sem refazer a conta
 * quebra aqui, em vez de passar despercebido.
 *
 * Limiares da WCAG 2.1: 4.5:1 para texto normal (1.4.3) e 3:1 para componentes
 * de interface e indicadores de estado, o foco incluído (1.4.11).
 */

const TOKENS = readColorTokens(readFileSync('src/styles.css', 'utf8'));

function color(name: string): string {
  const value = TOKENS[name];
  if (!value) {
    throw new Error(`Token --${name} não existe em src/styles.css`);
  }
  return value;
}

/** [frente, fundo, mínimo, onde aparece na tela] */
const TEXT_PAIRS: readonly [string, string, number, string][] = [
  ['ink', 'paper', 4.5, 'corpo sobre a página'],
  ['ink', 'panel', 4.5, 'corpo sobre o painel'],
  ['ink', 'accent-soft', 4.5, 'rótulo da opção selecionada'],
  ['ink', 'danger-soft', 4.5, 'texto dentro do bloco de erro'],
  ['ink-soft', 'paper', 4.5, 'texto secundário sobre a página'],
  ['ink-soft', 'panel', 4.5, 'dica e descrição sobre o painel'],
  ['ink-soft', 'accent-soft', 4.5, 'descrição na opção selecionada'],
  ['accent', 'panel', 4.5, 'nome do projeto e diretórios no resumo'],
  ['accent', 'paper', 4.5, 'acento sobre a página'],
  ['accent', 'accent-soft', 4.5, 'título do aviso de download concluído'],
  ['on-accent', 'accent', 4.5, 'texto do botão primário'],
  ['on-accent', 'accent-strong', 4.5, 'texto do botão primário sob o ponteiro'],
  ['on-accent', 'ink-soft', 4.5, 'texto do botão bloqueado durante a geração'],
  ['danger', 'panel', 4.5, 'erro inline e motivo da opção indisponível'],
  ['danger', 'paper', 4.5, 'motivo na opção indisponível, que fica recessada'],
  ['danger', 'danger-soft', 4.5, 'título do aviso de falha na geração'],
  ['danger', 'accent-soft', 4.5, 'erro sobre a linha selecionada'],
];

/**
 * Pares decorativos, isentos de 1.4.11 por decisão registrada em
 * `docs/design/visual-spec.md`: `--rule` desenha divisória e contorno de caixa
 * (`.masthead`, `.panel`, `.field + .field`, `.options`, `.option + .option`,
 * `.actions`, o resumo e o aviso de catálogo indisponível) e nunca é a única
 * pista de um agrupamento — quem carrega essa informação é o `<fieldset>` com
 * sua `<legend>`, o cabeçalho de cada bloco e a própria ordem da tela.
 *
 * Estão listados aqui para que os pares contados e os pares existentes sejam o
 * mesmo conjunto: `--rule` aparece sobre estas três superfícies e sobre mais
 * nenhuma.
 */
const DECORATIVE_PAIRS: readonly [string, string, string][] = [
  ['rule', 'panel', 'divisórias e contornos dentro dos painéis'],
  ['rule', 'paper', 'régua do cabeçalho e separadores sobre a página'],
  ['rule', 'accent-soft', 'separador entre opções quando a linha está selecionada'],
];

const UI_PAIRS: readonly [string, string, number, string][] = [
  ['accent', 'panel', 3, 'indicador de foco sobre o painel'],
  ['accent', 'paper', 3, 'indicador de foco sobre a página'],
  ['accent', 'accent-soft', 3, 'indicador de foco sobre a linha selecionada'],
  ['accent', 'danger-soft', 3, 'indicador de foco sobre o bloco de erro'],
  ['rule-strong', 'panel', 3, 'borda do campo de texto e guia da árvore'],
  ['rule-strong', 'paper', 3, 'guia da árvore sobre a página'],
  ['rule-strong', 'accent-soft', 3, 'borda de controle sobre linha selecionada'],
];

describe('tokens de cor', () => {
  it('define todos os tokens que a tela usa', () => {
    const required = [
      'paper',
      'panel',
      'ink',
      'ink-soft',
      'accent',
      'accent-strong',
      'accent-soft',
      'on-accent',
      'rule',
      'rule-strong',
      'danger',
      'danger-soft',
    ];
    expect(Object.keys(TOKENS)).toEqual(expect.arrayContaining(required));
  });

  for (const [front, back, minimum, where] of TEXT_PAIRS) {
    it(`texto AA (≥ ${minimum}:1): ${where}`, () => {
      expect(contrastRatio(color(front), color(back))).toBeGreaterThanOrEqual(minimum);
    });
  }

  for (const [front, back, minimum, where] of UI_PAIRS) {
    it(`elemento de interface (≥ ${minimum}:1): ${where}`, () => {
      expect(contrastRatio(color(front), color(back))).toBeGreaterThanOrEqual(minimum);
    });
  }

  for (const [front, back, where] of DECORATIVE_PAIRS) {
    it(`decorativo, isento e declarado: ${where}`, () => {
      // A isenção é declarada, não herdada. Se este par passar de 3:1, ela
      // deixou de ser necessária: mova-o para UI_PAIRS em vez de alargá-la.
      expect(contrastRatio(color(front), color(back))).toBeLessThan(3);
    });
  }

  it('calcula a razão do jeito que a WCAG define', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 5);
    expect(contrastRatio('#FFFFFF', '#FFFFFF')).toBeCloseTo(1, 5);
    // Valor de referência conhecido: cinza web sobre branco.
    expect(contrastRatio('#808080', '#FFFFFF')).toBeCloseTo(3.95, 2);
  });
});

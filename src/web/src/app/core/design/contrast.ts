/**
 * Razão de contraste da WCAG 2.1, para que a afirmação "AA" seja uma conta e
 * não uma impressão visual.
 *
 * Fórmulas: luminância relativa (WCAG 2.1, definição "relative luminance") e
 * razão de contraste (definição "contrast ratio").
 */

/** Converte `#rrggbb` nos três canais normalizados em 0…1. */
function channels(hex: string): [number, number, number] {
  const digits = hex.trim().replace('#', '');
  if (!/^[0-9a-fA-F]{6}$/.test(digits)) {
    throw new Error(`Cor fora do formato #rrggbb: "${hex}"`);
  }
  return [0, 2, 4].map((start) => parseInt(digits.slice(start, start + 2), 16) / 255) as [
    number,
    number,
    number,
  ];
}

function linearize(channel: number): number {
  return channel <= 0.04045 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
}

export function relativeLuminance(hex: string): number {
  const [r, g, b] = channels(hex).map(linearize);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** A razão entre duas cores, de 1 (iguais) a 21 (preto sobre branco). */
export function contrastRatio(a: string, b: string): number {
  const [lighter, darker] = [relativeLuminance(a), relativeLuminance(b)].sort((x, y) => y - x);
  return (lighter + 0.05) / (darker + 0.05);
}

/** Lê os tokens `--nome: #valor;` de uma folha de estilo. */
export function readColorTokens(css: string): Readonly<Record<string, string>> {
  const tokens: Record<string, string> = {};
  for (const match of css.matchAll(/--([a-z0-9-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    tokens[match[1]] = match[2];
  }
  return tokens;
}

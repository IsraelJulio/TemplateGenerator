import { diagnoseCatalogFailure } from './catalog-diagnosis';

describe('diagnóstico do catálogo indisponível', () => {
  it('diz "API fora do ar" quando nem o health respondeu', () => {
    const failure = diagnoseCatalogFailure(0, { reached: false, status: 0 });

    expect(failure.kind).toBe('api-unreachable');
    expect(failure.title).toContain('não respondeu');
    expect(failure.evidence).toEqual([
      { call: 'GET /api/health', result: 'sem resposta' },
      { call: 'GET /api/template-options', result: 'sem resposta' },
    ]);
  });

  it('distingue "rota ausente" de "API fora do ar" — o ponto de existir o health', () => {
    const rotaAusente = diagnoseCatalogFailure(404, { reached: true, status: 200 });
    const foraDoAr = diagnoseCatalogFailure(404, { reached: false, status: 0 });

    expect(rotaAusente.kind).toBe('route-missing');
    expect(rotaAusente.title).toContain('está no ar');
    expect(rotaAusente.title).toContain('não existe');

    // O MESMO 404 no catálogo, lido de outro jeito porque o health mudou.
    expect(foraDoAr.kind).toBe('api-unreachable');
    expect(foraDoAr.title).not.toEqual(rotaAusente.title);
  });

  it('lê 502/503/504 no health como API fora do ar, não como outro serviço', () => {
    // Com o `ng serve` na frente, derrubar a API responde 502 pelo proxy: o
    // navegador nunca vê conexão recusada. Observado em navegador, em T02.
    for (const status of [502, 503, 504]) {
      const failure = diagnoseCatalogFailure(status, { reached: true, status });
      expect(failure.kind).toBe('api-unreachable');
      expect(failure.evidence[0].result).toBe(`HTTP ${status}`);
    }
  });

  it('não confunde outro serviço com a API do gerador', () => {
    const failure = diagnoseCatalogFailure(404, { reached: true, status: 404 });

    expect(failure.kind).toBe('not-the-generator-api');
    expect(failure.evidence[0].result).toBe('HTTP 404');
  });

  it('separa "a rota existe e falhou" de "a rota não existe"', () => {
    const falhou = diagnoseCatalogFailure(500, { reached: true, status: 200 });

    expect(falhou.kind).toBe('catalog-failed');
    expect(falhou.evidence[1].result).toBe('HTTP 500');
  });

  it('trata qualquer 2xx do health como API viva', () => {
    for (const status of [200, 204, 299]) {
      expect(diagnoseCatalogFailure(503, { reached: true, status }).kind).toBe('catalog-failed');
    }
  });

  it('sempre diz o que fazer a seguir', () => {
    const casos = [
      diagnoseCatalogFailure(0, { reached: false, status: 0 }),
      diagnoseCatalogFailure(404, { reached: true, status: 200 }),
      diagnoseCatalogFailure(500, { reached: true, status: 200 }),
      diagnoseCatalogFailure(404, { reached: true, status: 500 }),
    ];

    for (const caso of casos) {
      expect(caso.hint.length).toBeGreaterThan(10);
      expect(caso.evidence).toHaveLength(2);
    }
  });
});

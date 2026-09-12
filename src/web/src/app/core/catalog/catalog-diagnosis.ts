/**
 * Por que o catálogo não carregou.
 *
 * Sozinho, o `GET /api/template-options` não distingue "a API está fora do ar"
 * de "a API está no ar e essa rota não existe": as duas coisas chegam ao
 * frontend como uma chamada que falhou. `GET /api/health` existe exatamente
 * para desempatar (ver `docs/architecture/http-contract.md`), e é esta sondagem
 * que esta função interpreta.
 */

/** O que a sondagem de `GET /api/health` observou. */
export interface HealthProbe {
  /**
   * `false` quando nem houve resposta HTTP — conexão recusada, DNS, rede.
   * O Angular entrega `status === 0` nesse caso.
   */
  readonly reached: boolean;
  /** O status observado; `0` quando `reached` é `false`. */
  readonly status: number;
}

export type CatalogFailureKind =
  /** Nada respondeu no endereço da API. */
  | 'api-unreachable'
  /** Alguma coisa respondeu, mas o `/api/health` não está lá ou não está bem. */
  | 'not-the-generator-api'
  /** A API responde, e a rota do catálogo não existe. */
  | 'route-missing'
  /** A API responde, a rota existe e o catálogo falhou mesmo assim. */
  | 'catalog-failed';

/** Uma chamada sondada e o que ela devolveu. */
export interface ProbeEvidence {
  readonly call: string;
  readonly result: string;
}

export interface CatalogFailure {
  readonly kind: CatalogFailureKind;
  /** Uma frase, em português, dizendo o que aconteceu. */
  readonly title: string;
  /** O que fazer a respeito. */
  readonly hint: string;
  /** O que foi observado, para quem for investigar. */
  readonly evidence: readonly ProbeEvidence[];
}

function describeStatus(status: number): string {
  return status === 0 ? 'sem resposta' : `HTTP ${status}`;
}

/**
 * Status de gateway: quem respondeu foi um intermediário dizendo que *não*
 * conseguiu falar com a API.
 *
 * Isto não é teoria: com o `ng serve` na frente, derrubar a API faz o proxy
 * responder `502` — o navegador nunca vê a conexão recusada. Sem esta regra, o
 * caso mais comum de "API fora do ar" em desenvolvimento seria diagnosticado
 * como "isso aqui não é a API do gerador", que é a resposta errada.
 */
function isGatewayFailure(status: number): boolean {
  return status === 502 || status === 503 || status === 504;
}

function evidenceOf(healthStatus: number, optionsStatus: number): readonly ProbeEvidence[] {
  return [
    { call: 'GET /api/health', result: describeStatus(healthStatus) },
    { call: 'GET /api/template-options', result: describeStatus(optionsStatus) },
  ];
}

/**
 * Cruza a falha do catálogo com a sondagem de saúde.
 *
 * `optionsStatus` é o status de `GET /api/template-options` (`0` quando a
 * chamada nem chegou a receber resposta).
 */
export function diagnoseCatalogFailure(optionsStatus: number, health: HealthProbe): CatalogFailure {
  const evidence = evidenceOf(health.reached ? health.status : 0, optionsStatus);

  if (!health.reached || isGatewayFailure(health.status)) {
    return {
      kind: 'api-unreachable',
      title: 'A API do gerador não respondeu.',
      hint: 'Confira se ela está no ar e se o endereço configurado está certo.',
      evidence,
    };
  }

  if (health.status < 200 || health.status >= 300) {
    return {
      kind: 'not-the-generator-api',
      title: 'Alguma coisa respondeu nesse endereço, mas não é a API do gerador.',
      hint: 'O endereço pode apontar para outro serviço, ou a API subiu com defeito.',
      evidence,
    };
  }

  if (optionsStatus === 404) {
    return {
      kind: 'route-missing',
      title: 'A API está no ar, mas a rota do catálogo não existe.',
      hint: 'Provavelmente a API em execução é de uma versão anterior à desta tela.',
      evidence,
    };
  }

  return {
    kind: 'catalog-failed',
    title: 'A API está no ar, mas falhou ao montar o catálogo.',
    hint: 'Tente de novo; se insistir, o log da API é o próximo lugar a olhar.',
    evidence,
  };
}

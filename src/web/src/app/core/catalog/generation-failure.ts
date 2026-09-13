/**
 * Traduz uma geração que falhou em algo que a tela consegue mostrar sem perder
 * nenhuma escolha da pessoa (RF-06).
 *
 * Nada aqui decide se a configuração é válida — quem decide é o backend. Esta
 * função só lê o `ProblemDetails` que ele devolveu e separa o que vai inline,
 * no campo, do que vai no aviso geral.
 */

import { ProblemDetails } from './template-options.model';

export interface GenerationFailure {
  /** A frase principal do aviso. */
  readonly title: string;
  /** Uma linha a mais, quando a API mandou `detail`. */
  readonly detail: string | null;
  /** Mensagens endereçadas a campos, na forma do `errors` da RFC 9457. */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /**
   * `true` no `501` — a configuração passou pela validação, **esta combinação**
   * é que ainda não gera projeto. É um estado transitório do projeto, não um
   * erro da pessoa.
   *
   * **O escopo mudou em T04** (ADR-0012): não é mais "o motor de geração ainda
   * não existe", é "algum valor desta combinação ainda não tem template". E o
   * ramo deixou de ser código morto: a tela desabilita o indisponível, então o
   * `501` não é alcançável clicando — o caso real é o catálogo envelhecido numa
   * aba aberta antes de uma implantação, o mesmo motivo pelo qual o ramo do
   * `400` existe embora a tela valide.
   */
  readonly notImplemented: boolean;
  /** Segundos do `Retry-After`, quando a API pediu para esperar. */
  readonly retryAfterSeconds: number | null;
}

function readRetryAfter(header: string | null): number | null {
  if (!header) {
    return null;
  }
  const seconds = Number.parseInt(header.trim(), 10);
  return Number.isFinite(seconds) && seconds >= 0 ? seconds : null;
}

export function describeGenerationFailure(
  status: number,
  problem: ProblemDetails | null,
  retryAfterHeader: string | null = null,
): GenerationFailure {
  const fieldErrors = problem?.errors ?? {};
  const detail = problem?.detail ?? null;
  const retryAfterSeconds = readRetryAfter(retryAfterHeader);

  if (status === 0) {
    return {
      title: 'A API não respondeu. Suas escolhas continuam aqui.',
      detail: null,
      fieldErrors: {},
      notImplemented: false,
      retryAfterSeconds: null,
    };
  }

  if (status === 501) {
    // Mesma forma do `400`, de propósito: o `501` endereça a recusa ao campo
    // indisponível em `errors`, e a tela já sabe posicionar isso inline. Por
    // isso `detail` some quando há erro de campo — e o `501` não manda `detail`
    // nenhum, então repassá-lo é só não inventar um.
    return {
      title: problem?.title ?? 'Esta combinação ainda não gera projeto',
      detail: Object.keys(fieldErrors).length > 0 ? null : detail,
      fieldErrors,
      notImplemented: true,
      retryAfterSeconds: null,
    };
  }

  if (status === 429) {
    return {
      title: problem?.title ?? 'A API está limitando as gerações no momento.',
      detail:
        retryAfterSeconds !== null
          ? `Tente de novo em ${retryAfterSeconds} segundo${retryAfterSeconds === 1 ? '' : 's'}.`
          : detail,
      fieldErrors,
      notImplemented: false,
      retryAfterSeconds,
    };
  }

  if (status === 400) {
    return {
      title: problem?.title ?? 'A API recusou a configuração.',
      detail: Object.keys(fieldErrors).length > 0 ? null : detail,
      fieldErrors,
      notImplemented: false,
      retryAfterSeconds: null,
    };
  }

  return {
    title: problem?.title ?? `A geração falhou (HTTP ${status}).`,
    detail,
    fieldErrors,
    notImplemented: false,
    retryAfterSeconds: null,
  };
}

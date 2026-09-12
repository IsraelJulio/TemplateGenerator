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
   * `true` no `501` — a configuração passou, o motor de geração ainda não
   * existe. É um estado transitório do projeto, não um erro da pessoa, e a
   * tela precisa dizer isso com essas palavras. Ver a seção "Estado
   * transitório: 501" de `docs/architecture/http-contract.md`.
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
    return {
      title: problem?.title ?? 'A geração ainda não foi implementada.',
      detail:
        detail ??
        'A configuração passou na validação da API, mas o motor que monta o ZIP ainda não existe.',
      fieldErrors: {},
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

import { readFileSync } from 'node:fs';
import {
  OptionValue,
  TemplateConstraint,
  TemplateField,
  TemplateOptionValue,
  TemplateOptionsCatalog,
  UnavailableOption,
} from '../core/catalog/template-options.model';

/**
 * As fixtures do catálogo, **derivadas** do snapshot versionado de
 * `GET /api/template-options` — critério de aceite 7 de T09.
 *
 * Nenhum rótulo, descrição, padrão ou restrição é escrito à mão aqui. O arquivo
 * `template-options.snapshot.json` é do backend: o teste
 * `tests/TemplateGenerator.Api.Tests/TemplateOptionsSnapshotTests.cs` cobra que
 * o payload real é **igual** a ele, e este módulo o lê a cada execução do
 * Vitest. Os dois juntos fecham o laço que em T01 ficou aberto, quando a
 * fixture escrita à mão divergiu do payload em três pontos sem derrubar teste
 * nenhum — os leitores toleravam as duas formas.
 *
 * Mesmo arranjo de `core/summary/zip-structure.spec.ts`: `readFileSync` +
 * `JSON.parse`, caminho relativo ao diretório de trabalho do Vitest
 * (`src/web/`), e `node:fs` declarado em `node-fs.d.ts`, que só vale no
 * programa dos testes.
 */

/** Relativo a `src/web`, que é o diretório de trabalho do Vitest. */
const SNAPSHOT_FILE = 'src/app/testing/template-options.snapshot.json';

/**
 * Onde o backend escreve a frase de `unavailable` — ver {@link UNAVAILABLE_REASON}.
 * Relativo a `src/web`, como o snapshot.
 */
const UNAVAILABLE_REASON_SOURCE = '../TemplateGenerator.Generation/Engine/TemplateAvailability.cs';

// ------------------------------------------------------------- validação

/**
 * Lê o JSON como `TemplateOptionsCatalog` **conferindo a forma**, em vez de só
 * fazer cast. Um cast aceitaria qualquer coisa, e a fixture voltaria a ser o
 * lugar onde uma deriva do payload passa calada: se o backend regravar o
 * snapshot com um campo sem `type`, por exemplo, é aqui que o Vitest cai, com o
 * caminho da chave errada na mensagem.
 *
 * Valida as regras de serialização de `http-contract.md` que o cliente depende:
 * `type` obrigatório e só `choice`/`boolean` (regra 3), `values` presente só no
 * de escolha, arrays em `when`/`requires` (regra 4) e todo par de `unavailable`
 * localizável em `fields`.
 */
function fail(path: string, expected: string, got: unknown): never {
  throw new Error(
    `${SNAPSHOT_FILE}: "${path}" deveria ser ${expected}, veio ${JSON.stringify(got)}.`,
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function text(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.trim() === '') {
    fail(path, 'texto não vazio', value);
  }
  return value;
}

function optionalText(value: unknown, path: string): string | undefined {
  return value === undefined ? undefined : text(value, path);
}

function optionValue(value: unknown, path: string): OptionValue {
  if (typeof value !== 'string' && typeof value !== 'boolean') {
    fail(path, 'texto ou booleano', value);
  }
  return value;
}

function parseOption(raw: unknown, path: string): TemplateOptionValue {
  if (!isRecord(raw)) fail(path, 'objeto', raw);
  const description = optionalText(raw['description'], `${path}.description`);
  return {
    value: text(raw['value'], `${path}.value`),
    label: text(raw['label'], `${path}.label`),
    ...(description === undefined ? {} : { description }),
  };
}

function parseField(raw: unknown, path: string): TemplateField {
  if (!isRecord(raw)) fail(path, 'objeto', raw);

  const label = text(raw['label'], `${path}.label`);
  const description = optionalText(raw['description'], `${path}.description`);
  const type = raw['type'];
  const fallback = raw['default'];

  if (type === 'boolean') {
    if (typeof fallback !== 'boolean') fail(`${path}.default`, 'booleano', fallback);
    if (raw['values'] !== undefined) fail(`${path}.values`, 'ausente num booleano', raw['values']);
    return {
      label,
      type,
      default: fallback,
      ...(description === undefined ? {} : { description }),
    };
  }

  if (type !== 'choice') fail(`${path}.type`, '"choice" ou "boolean"', type);

  const rawValues = raw['values'];
  if (!Array.isArray(rawValues) || rawValues.length === 0) {
    fail(`${path}.values`, 'lista não vazia', rawValues);
  }
  const values = rawValues.map((option, i) => parseOption(option, `${path}.values[${i}]`));
  const defaultValue = text(fallback, `${path}.default`);
  if (!values.some((option) => option.value === defaultValue)) {
    fail(`${path}.default`, 'um dos valores do próprio campo', defaultValue);
  }

  return {
    label,
    type,
    default: defaultValue,
    values,
    ...(description === undefined ? {} : { description }),
  };
}

function parseMatches(
  raw: unknown,
  path: string,
): Readonly<Record<string, readonly OptionValue[]>> {
  if (!isRecord(raw)) fail(path, 'objeto', raw);
  const matches: Record<string, readonly OptionValue[]> = {};
  for (const [field, accepted] of Object.entries(raw)) {
    // Regra 4: o backend **emite** sempre array. A leitura tolerante é da tela,
    // não do snapshot — aqui o valor único é defeito do payload.
    if (!Array.isArray(accepted) || accepted.length === 0) {
      fail(`${path}.${field}`, 'lista não vazia (regra 4)', accepted);
    }
    matches[field] = accepted.map((value, i) => optionValue(value, `${path}.${field}[${i}]`));
  }
  return matches;
}

function parseConstraint(raw: unknown, path: string): TemplateConstraint {
  if (!isRecord(raw)) fail(path, 'objeto', raw);
  return {
    id: text(raw['id'], `${path}.id`),
    when: parseMatches(raw['when'], `${path}.when`),
    requires: parseMatches(raw['requires'], `${path}.requires`),
    message: text(raw['message'], `${path}.message`),
  };
}

function parseUnavailable(raw: unknown, path: string): UnavailableOption {
  if (!isRecord(raw)) fail(path, 'objeto', raw);
  return {
    field: text(raw['field'], `${path}.field`),
    value: optionValue(raw['value'], `${path}.value`),
    reason: text(raw['reason'], `${path}.reason`),
  };
}

/** Todo par `(campo, valor)` citado tem de existir em `fields`, no tipo do campo. */
function assertBelongs(
  fields: Readonly<Record<string, TemplateField>>,
  field: string,
  value: OptionValue,
  path: string,
): void {
  const definition = fields[field];
  if (!definition) fail(path, 'um campo do catálogo', field);

  const known =
    definition.type === 'boolean'
      ? typeof value === 'boolean'
      : (definition.values ?? []).some((option) => option.value === value);
  if (!known) fail(path, `um valor de "${field}" no tipo do campo`, value);
}

export function parseCatalog(raw: unknown): TemplateOptionsCatalog {
  if (!isRecord(raw)) fail('$', 'objeto', raw);

  const rawFields = raw['fields'];
  if (!isRecord(rawFields) || Object.keys(rawFields).length === 0) {
    fail('fields', 'objeto com ao menos um campo', rawFields);
  }
  const fields: Record<string, TemplateField> = {};
  for (const [key, field] of Object.entries(rawFields)) {
    fields[key] = parseField(field, `fields.${key}`);
  }

  const rawConstraints = raw['constraints'];
  if (!Array.isArray(rawConstraints)) fail('constraints', 'lista', rawConstraints);
  const constraints = rawConstraints.map((item, i) => parseConstraint(item, `constraints[${i}]`));
  constraints.forEach((constraint, i) => {
    for (const side of ['when', 'requires'] as const) {
      for (const [field, values] of Object.entries(constraint[side])) {
        (values as readonly OptionValue[]).forEach((value: OptionValue, j: number) =>
          assertBelongs(fields, field, value, `constraints[${i}].${side}.${field}[${j}]`),
        );
      }
    }
  });

  const rawUnavailable = raw['unavailable'];
  if (!Array.isArray(rawUnavailable)) fail('unavailable', 'lista (sempre emitida)', rawUnavailable);
  const unavailable = rawUnavailable.map((item, i) => parseUnavailable(item, `unavailable[${i}]`));
  unavailable.forEach((entry, i) =>
    assertBelongs(fields, entry.field, entry.value, `unavailable[${i}]`),
  );

  return {
    templateVersion: text(raw['templateVersion'], 'templateVersion'),
    fields,
    constraints,
    unavailable,
  };
}

/** Congela em profundidade: um spec que mutasse a fixture contaminaria os outros. */
function deepFreeze<T>(value: T): T {
  if (typeof value === 'object' && value !== null) {
    for (const nested of Object.values(value)) deepFreeze(nested);
    Object.freeze(value);
  }
  return value;
}

// ------------------------------------------------------------- fixtures

/**
 * O catálogo **exatamente** como `GET /api/template-options` o emite hoje: o
 * snapshot, lido e validado, sem retoque. Com `"unavailable": []` desde T08 —
 * todo valor tem template.
 */
export const SNAPSHOT_CATALOG: TemplateOptionsCatalog = deepFreeze(
  parseCatalog(JSON.parse(readFileSync(SNAPSHOT_FILE, 'utf8'))),
);

/** O nome que os specs já usam para "tudo implementado". É o snapshot puro. */
export const IMPLEMENTED_CATALOG_FIXTURE: TemplateOptionsCatalog = SNAPSHOT_CATALOG;

/**
 * A frase única que o backend repete para todo valor sem template — em
 * `unavailable` e no `errors` do `501` (ADR-0012).
 *
 * **Fonte:** a constante `TemplateAvailability.UnavailableReason` do próprio
 * backend, lida do código-fonte a cada execução. O snapshot não serve de fonte
 * porque, com tudo implementado, `unavailable` chega vazio e a frase não aparece
 * nele; escrevê-la aqui de novo seria a segunda cópia que ADR-0012 existe para
 * não criar. Se a constante mudar de nome ou de forma, este módulo cai na
 * carga, com o caminho na mensagem — nunca com uma frase velha.
 */
export const UNAVAILABLE_REASON: string = (() => {
  const source = readFileSync(UNAVAILABLE_REASON_SOURCE, 'utf8');
  const match = /\bUnavailableReason\s*=\s*"((?:[^"\\]|\\.)*)"/.exec(source);
  if (!match) {
    throw new Error(
      `${UNAVAILABLE_REASON_SOURCE}: a constante UnavailableReason não foi encontrada.`,
    );
  }
  return match[1];
})();

/**
 * **Variação sintética** do snapshot: o mesmo catálogo, com dois valores
 * marcados como "sem template" para exercitar o estado da tela que a API de
 * hoje não produz mais (ADR-0012).
 *
 * A sobreposição é mínima e declarada: um valor de banco que não é o padrão, e
 * o valor que a restrição do catálogo recusa sem banco — o par que mostra a
 * indisponibilidade **vencendo** a restrição no mesmo valor. Os dois são
 * conferidos contra o snapshot por `parseCatalog` (todo par pertence ao
 * catálogo), então a variação não consegue citar uma opção que não existe.
 */
const SYNTHETIC_UNAVAILABLE: readonly Omit<UnavailableOption, 'reason'>[] = [
  { field: 'database', value: 'sqlite' },
  { field: 'authentication', value: 'identity' },
];

export const CATALOG_FIXTURE: TemplateOptionsCatalog = deepFreeze(
  parseCatalog({
    ...SNAPSHOT_CATALOG,
    unavailable: SYNTHETIC_UNAVAILABLE.map((entry) => ({ ...entry, reason: UNAVAILABLE_REASON })),
  }),
);

/**
 * Um payload que **quebra** as regras de serialização, montado a partir do
 * snapshot: nenhum campo traz `type`, toda lista de um valor só em
 * `when`/`requires` vira valor escalar, e `unavailable` some.
 *
 * Nada envia isso hoje. Existe para fixar que a **leitura** continua tolerante
 * — e por isso entra por cast: o modelo não chama esta forma de legal.
 */
export const MALFORMED_CATALOG_FIXTURE = (() => {
  const fields = Object.fromEntries(
    Object.entries(SNAPSHOT_CATALOG.fields).map(([key, field]) => {
      const { type: _type, ...untyped } = field;
      return [key, untyped];
    }),
  );
  const scalar = (matches: TemplateConstraint['when']) =>
    Object.fromEntries(
      Object.entries(matches).map(([field, accepted]) => [
        field,
        Array.isArray(accepted) && accepted.length === 1 ? accepted[0] : accepted,
      ]),
    );

  return deepFreeze({
    templateVersion: SNAPSHOT_CATALOG.templateVersion,
    fields,
    constraints: SNAPSHOT_CATALOG.constraints.map((constraint) => ({
      ...constraint,
      when: scalar(constraint.when),
      requires: scalar(constraint.requires),
    })),
  }) as unknown as TemplateOptionsCatalog;
})();

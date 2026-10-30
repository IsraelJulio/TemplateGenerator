import { readFileSync } from 'node:fs';
import {
  CATALOG_FIXTURE,
  IMPLEMENTED_CATALOG_FIXTURE,
  SNAPSHOT_CATALOG,
  UNAVAILABLE_REASON,
  parseCatalog,
} from './catalog.fixture';

/**
 * A fixture é o snapshot, e não uma cópia dele — critério de aceite 7 de T09.
 *
 * Estes testes guardam a derivação: se alguém voltasse a escrever o catálogo à
 * mão aqui, ou se a validação de forma deixasse de recusar o que o contrato
 * proíbe, a deriva de T01 voltaria a passar calada.
 */
const RAW = JSON.parse(readFileSync('src/app/testing/template-options.snapshot.json', 'utf8'));

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

describe('fixture do catálogo', () => {
  it('é o snapshot versionado, sem retoque', () => {
    expect(SNAPSHOT_CATALOG).toEqual(RAW);
    expect(IMPLEMENTED_CATALOG_FIXTURE).toBe(SNAPSHOT_CATALOG);
  });

  it('a variação sintética difere do snapshot só em `unavailable`', () => {
    const { unavailable, ...resto } = CATALOG_FIXTURE;
    const { unavailable: _real, ...snapshotResto } = SNAPSHOT_CATALOG;

    expect(resto).toEqual(snapshotResto);
    expect(unavailable.length).toBeGreaterThan(0);
    for (const entry of unavailable) {
      expect(entry.reason).toBe(UNAVAILABLE_REASON);
    }
  });

  it('lê a frase de `unavailable` do backend, não de uma cópia', () => {
    expect(UNAVAILABLE_REASON.trim().length).toBeGreaterThan(0);
    expect(
      readFileSync('../TemplateGenerator.Generation/Engine/TemplateAvailability.cs', 'utf8'),
    ).toContain(`"${UNAVAILABLE_REASON}"`);
  });

  it('está congelada, para um spec não contaminar o outro', () => {
    expect(Object.isFrozen(SNAPSHOT_CATALOG.fields)).toBe(true);
    expect(Object.isFrozen(CATALOG_FIXTURE.unavailable)).toBe(true);
  });
});

describe('validação de forma do snapshot', () => {
  it('recusa campo sem `type` (regra 3)', () => {
    const raw = clone(RAW);
    const [key] = Object.keys(raw.fields);
    delete raw.fields[key].type;

    expect(() => parseCatalog(raw)).toThrow(`fields.${key}.type`);
  });

  it('recusa restrição com valor escalar (regra 4)', () => {
    const raw = clone(RAW);
    const when = raw.constraints[0].when;
    const [field] = Object.keys(when);
    when[field] = when[field][0];

    expect(() => parseCatalog(raw)).toThrow(`constraints[0].when.${field}`);
  });

  it('recusa par de `unavailable` que o catálogo não tem', () => {
    const raw = clone(RAW);
    const [field] = Object.keys(raw.fields);
    raw.unavailable = [{ field, value: 'valor-que-nao-existe', reason: 'x' }];

    expect(() => parseCatalog(raw)).toThrow('unavailable[0]');
  });

  it('recusa catálogo sem `unavailable`, que a API sempre emite', () => {
    const raw = clone(RAW);
    delete raw.unavailable;

    expect(() => parseCatalog(raw)).toThrow('unavailable');
  });

  it('recusa padrão que não está entre os valores do campo', () => {
    const raw = clone(RAW);
    const key = Object.keys(raw.fields).find((name) => raw.fields[name].type === 'choice')!;
    raw.fields[key].default = 'valor-que-nao-existe';

    expect(() => parseCatalog(raw)).toThrow(`fields.${key}.default`);
  });

  it('aceita a opção sem `description`: o cliente tolera a ausência (regra 8 do contrato)', () => {
    // Desde T10 o contrato exige `description` em todo valor de escolha e o
    // snapshot cumpre; a tolerância do cliente é provada removendo-a de um valor.
    const raw = clone(RAW);
    const field = Object.values(raw.fields as Record<string, { values?: { description?: string }[] }>)
      .find((candidate) => (candidate.values?.length ?? 0) > 0)!;
    delete field.values![0].description;

    expect(() => parseCatalog(raw)).not.toThrow();
  });

  it('o snapshot traz `description` em todo valor de escolha (docs/architecture/http-contract.md)', () => {
    const semDescricao = Object.values(SNAPSHOT_CATALOG.fields)
      .flatMap((field) => field.values ?? [])
      .filter((option) => !option.description?.trim());

    expect(semDescricao).toEqual([]);
  });
});

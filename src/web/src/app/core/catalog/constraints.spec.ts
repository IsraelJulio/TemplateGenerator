import { CATALOG_FIXTURE, MALFORMED_CATALOG_FIXTURE } from '../../testing/catalog.fixture';
import { evaluateAvailability, findViolations, violationsByField } from './constraints';
import { TemplateOptionsCatalog } from './template-options.model';

const DEFAULTS = {
  architecture: 'simple',
  database: 'none',
  authentication: 'none',
  swagger: true,
  dotnetVersion: 'net10.0',
};

function availabilityOf(
  catalog: TemplateOptionsCatalog,
  selection: Record<string, string | boolean>,
  field: string,
  value: string | boolean,
) {
  return evaluateAvailability(catalog, selection)[field].find((option) => option.value === value);
}

describe('constraints', () => {
  it('não acusa violação na seleção padrão', () => {
    expect(findViolations(CATALOG_FIXTURE, DEFAULTS)).toEqual([]);
  });

  it('acusa violação quando a seleção quebra uma regra do catálogo', () => {
    const violations = findViolations(CATALOG_FIXTURE, {
      ...DEFAULTS,
      authentication: 'identity',
    });

    expect(violations).toHaveLength(1);
    expect(violations[0].constraint.id).toBe('identity-requires-database');
    expect(violations[0].fields).toEqual(['authentication']);
  });

  it('agrupa a mensagem sob o campo que disparou a regra', () => {
    const grouped = violationsByField(CATALOG_FIXTURE, {
      ...DEFAULTS,
      authentication: 'identity',
    });

    expect(grouped).toEqual({
      authentication: ['O Identity nativo precisa de um banco para persistir os usuários.'],
    });
  });

  it('desabilita a opção incompatível e informa o motivo do catálogo', () => {
    const identity = availabilityOf(CATALOG_FIXTURE, DEFAULTS, 'authentication', 'identity');

    expect(identity?.disabled).toBe(true);
    expect(identity?.reason).toBe(
      'O Identity nativo precisa de um banco para persistir os usuários.',
    );
  });

  it('libera a opção assim que o restante da seleção satisfaz a regra', () => {
    const identity = availabilityOf(
      CATALOG_FIXTURE,
      { ...DEFAULTS, database: 'sqlite' },
      'authentication',
      'identity',
    );

    expect(identity?.disabled).toBe(false);
    expect(identity?.reason).toBeNull();
  });

  it('aplica a regra nos dois sentidos, sem conhecer os campos envolvidos', () => {
    const none = availabilityOf(
      CATALOG_FIXTURE,
      { ...DEFAULTS, database: 'sqlite', authentication: 'identity' },
      'database',
      'none',
    );

    expect(none?.disabled).toBe(true);
  });

  it('não desabilita nada quando o catálogo não traz restrição', () => {
    const semRestricoes: TemplateOptionsCatalog = { ...CATALOG_FIXTURE, constraints: [] };
    const availability = evaluateAvailability(semRestricoes, {
      ...DEFAULTS,
      authentication: 'identity',
    });

    expect(availability['authentication'].every((option) => !option.disabled)).toBe(true);
  });

  it('interpreta uma restrição nova sem alteração de código', () => {
    const catalogoAmpliado: TemplateOptionsCatalog = {
      ...CATALOG_FIXTURE,
      constraints: [
        ...CATALOG_FIXTURE.constraints,
        {
          id: 'clean-requires-swagger',
          when: { architecture: ['clean'] },
          requires: { swagger: [true] },
          message: 'Regra hipotética, definida só no catálogo.',
        },
      ],
    };

    const clean = availabilityOf(
      catalogoAmpliado,
      { ...DEFAULTS, swagger: false },
      'architecture',
      'clean',
    );

    expect(clean?.disabled).toBe(true);
    expect(clean?.reason).toBe('Regra hipotética, definida só no catálogo.');
  });

  it('avalia os dois valores de um campo booleano', () => {
    const availability = evaluateAvailability(CATALOG_FIXTURE, DEFAULTS);

    expect(availability['swagger'].map((option) => option.value)).toEqual([true, false]);
  });

  it('tolera na leitura uma restrição em forma escalar, que o backend não emite', () => {
    const clean = availabilityOf(
      MALFORMED_CATALOG_FIXTURE,
      { architecture: 'simple', swagger: false },
      'architecture',
      'clean',
    );

    expect(clean?.disabled).toBe(true);
    expect(clean?.reason).toBe('Regra hipotética, em forma escalar.');
  });
});

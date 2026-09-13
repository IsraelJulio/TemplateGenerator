import {
  CATALOG_FIXTURE,
  IMPLEMENTED_CATALOG_FIXTURE,
  MALFORMED_CATALOG_FIXTURE,
  UNAVAILABLE_REASON,
} from '../../testing/catalog.fixture';
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
    // Catálogo com tudo implementado: hoje todo valor que uma restrição recusa é
    // também um valor sem template, e a indisponibilidade vence. Sem esta
    // variante não sobraria como exercitar a restrição sozinha — ver o bloco
    // "disponibilidade" adiante, que exercita a outra origem.
    const identity = availabilityOf(
      IMPLEMENTED_CATALOG_FIXTURE,
      DEFAULTS,
      'authentication',
      'identity',
    );

    expect(identity?.disabled).toBe(true);
    expect(identity?.reason).toBe(
      'O Identity nativo precisa de um banco para persistir os usuários.',
    );
  });

  it('libera a opção assim que o restante da seleção satisfaz a regra', () => {
    const identity = availabilityOf(
      IMPLEMENTED_CATALOG_FIXTURE,
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
    const semRestricoes: TemplateOptionsCatalog = {
      ...IMPLEMENTED_CATALOG_FIXTURE,
      constraints: [],
    };
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

/**
 * A segunda origem de "desabilitado" (ADR-0012): o membro de topo `unavailable`
 * do catálogo, que diz quais valores ainda não geram projeto. A forma de saída
 * é a mesma da restrição — `{ value, disabled, reason }` —, de propósito: a
 * marcação da tela não precisou mudar.
 */
describe('disponibilidade', () => {
  it('desabilita o valor sem template e mostra a razão que o catálogo mandou', () => {
    const sqlite = availabilityOf(CATALOG_FIXTURE, DEFAULTS, 'database', 'sqlite');

    expect(sqlite?.disabled).toBe(true);
    expect(sqlite?.reason).toBe(UNAVAILABLE_REASON);
  });

  it('deixa em paz o valor que tem template', () => {
    const none = availabilityOf(CATALOG_FIXTURE, DEFAULTS, 'database', 'none');

    expect(none?.disabled).toBe(false);
    expect(none?.reason).toBeNull();
  });

  it('faz a indisponibilidade vencer a restrição no mesmo valor', () => {
    // `identity` é recusado pelas duas origens ao mesmo tempo. A restrição diria
    // "escolha um banco", e escolher um banco não faria a opção gerar nada —
    // por isso quem fala é a indisponibilidade, que vale faça a pessoa o que
    // fizer.
    const semBanco = availabilityOf(CATALOG_FIXTURE, DEFAULTS, 'authentication', 'identity');
    expect(semBanco?.reason).toBe(UNAVAILABLE_REASON);

    const comBanco = availabilityOf(
      CATALOG_FIXTURE,
      { ...DEFAULTS, database: 'sqlite' },
      'authentication',
      'identity',
    );
    expect(comBanco?.disabled).toBe(true);
    expect(comBanco?.reason).toBe(UNAVAILABLE_REASON);
  });

  it('casa o valor no tipo do campo, inclusive no interruptor', () => {
    // O eixo booleano não tem `values`, e é por isso que o membro mora no topo
    // do catálogo: se um dia `swagger` ligado ficar sem template, é assim que a
    // tela desabilita o interruptor. Nada aqui conhece o campo nem o valor.
    const semSwagger: TemplateOptionsCatalog = {
      ...CATALOG_FIXTURE,
      unavailable: [{ field: 'swagger', value: true, reason: UNAVAILABLE_REASON }],
    };

    expect(availabilityOf(semSwagger, DEFAULTS, 'swagger', true)?.disabled).toBe(true);
    expect(availabilityOf(semSwagger, DEFAULTS, 'swagger', false)?.disabled).toBe(false);
  });

  it('some inteira quando está tudo implementado', () => {
    // `"unavailable": []` é o estado final do produto. O que precisa sumir é
    // esta origem, não toda desabilitação: a restrição continua valendo, e
    // `identity` sem banco segue recusada — por ela.
    const availability = evaluateAvailability(IMPLEMENTED_CATALOG_FIXTURE, DEFAULTS);
    const motivos = Object.values(availability).flatMap((options) =>
      options.map((option) => option.reason),
    );

    expect(motivos).not.toContain(UNAVAILABLE_REASON);
    expect(
      availabilityOf(IMPLEMENTED_CATALOG_FIXTURE, DEFAULTS, 'database', 'sqlite')?.disabled,
    ).toBe(false);
  });
});

import { TemplateOptionsCatalog } from '../core/catalog/template-options.model';

/**
 * The catalog exactly as `GET /api/template-options` emits it today: `type` on
 * every field and arrays on both sides of every constraint, per the
 * serialization rules of `docs/architecture/http-contract.md`.
 *
 * It exists to stand in for the real response, so it must keep tracking it. A
 * test that passes against a fixture the API no longer sends is worth nothing.
 */
/**
 * The one sentence the API repeats for every value without a template. It is
 * derived over there — same phrase in `unavailable` and in the `errors` of the
 * `501` — so it is written once here too.
 */
export const UNAVAILABLE_REASON = 'O template desta opção ainda não foi escrito.';

export const CATALOG_FIXTURE: TemplateOptionsCatalog = {
  templateVersion: '1.0.0',
  fields: {
    architecture: {
      label: 'Arquitetura',
      type: 'choice',
      default: 'simple',
      values: [
        { value: 'simple', label: 'Simples', description: 'Um projeto Web API.' },
        {
          value: 'clean',
          label: 'Clean Architecture',
          description: 'Api, Application, Domain e Infrastructure.',
        },
      ],
    },
    database: {
      label: 'Banco',
      type: 'choice',
      default: 'none',
      values: [
        { value: 'none', label: 'Nenhum', description: 'Sem persistência.' },
        { value: 'sqlite', label: 'SQLite', description: 'Arquivo local, sem serviço externo.' },
        {
          value: 'postgresql',
          label: 'PostgreSQL',
          description: 'Servidor PostgreSQL via Npgsql.',
        },
      ],
    },
    authentication: {
      label: 'Autenticação',
      type: 'choice',
      default: 'none',
      values: [
        { value: 'none', label: 'Nenhuma', description: 'Endpoints abertos.' },
        {
          value: 'identity',
          label: 'Identity',
          description: 'ASP.NET Core Identity, com usuários persistidos.',
        },
        {
          value: 'jwt',
          label: 'JWT',
          description: 'Validação de token, sem persistência de usuário.',
        },
      ],
    },
    swagger: {
      label: 'Swagger',
      type: 'boolean',
      default: true,
      description: 'Interface de exploração da API no projeto gerado.',
    },
    dotnetVersion: {
      label: 'Versão .NET',
      type: 'choice',
      default: 'net10.0',
      values: [{ value: 'net10.0', label: '.NET 10' }],
    },
  },
  constraints: [
    {
      id: 'identity-requires-database',
      when: { authentication: ['identity'] },
      requires: { database: ['sqlite', 'postgresql'] },
      message: 'O Identity nativo precisa de um banco para persistir os usuários.',
    },
  ],
  unavailable: [
    { field: 'database', value: 'sqlite', reason: UNAVAILABLE_REASON },
    { field: 'database', value: 'postgresql', reason: UNAVAILABLE_REASON },
    { field: 'authentication', value: 'identity', reason: UNAVAILABLE_REASON },
    { field: 'authentication', value: 'jwt', reason: UNAVAILABLE_REASON },
  ],
};

/**
 * The same catalog with everything implemented — `"unavailable": []`, which is
 * the shape the API will emit once the last fragment is written (T08).
 *
 * It exists because the two mechanisms overlap today: every value a constraint
 * rules out is also a value without a template, so with the real fixture there
 * is no way left to show a *constraint* being released by the rest of the
 * selection. This variant keeps that behaviour under test without pretending
 * the API sends something it does not.
 */
export const IMPLEMENTED_CATALOG_FIXTURE: TemplateOptionsCatalog = {
  ...CATALOG_FIXTURE,
  unavailable: [],
};

/**
 * A payload that breaks the serialization rules — no `type`, scalar `when`.
 * Nothing sends this today; it is here to pin down that reading stays tolerant,
 * which is why it has to be cast in: the model does not call this shape legal.
 */
export const MALFORMED_CATALOG_FIXTURE = {
  templateVersion: '1.0.0',
  fields: {
    architecture: {
      label: 'Arquitetura',
      default: 'simple',
      values: [
        { value: 'simple', label: 'Simples' },
        { value: 'clean', label: 'Clean Architecture' },
      ],
    },
    swagger: { label: 'Swagger', default: true },
  },
  constraints: [
    {
      id: 'clean-requires-swagger',
      when: { architecture: 'clean' },
      requires: { swagger: true },
      message: 'Regra hipotética, em forma escalar.',
    },
  ],
} as unknown as TemplateOptionsCatalog;

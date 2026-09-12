import { TemplateOptionsCatalog } from '../core/catalog/template-options.model';

/**
 * The catalog exactly as `GET /api/template-options` emits it today: `type` on
 * every field and arrays on both sides of every constraint, per the
 * serialization rules of `docs/architecture/http-contract.md`.
 *
 * It exists to stand in for the real response, so it must keep tracking it. A
 * test that passes against a fixture the API no longer sends is worth nothing.
 */
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

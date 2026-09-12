/**
 * ⚠️ PROJEÇÃO, não a verdade.
 *
 * Este é o **único** lugar do frontend que conhece valores de opção
 * (`clean`, `sqlite`, `identity`, …). Ele existe porque o catálogo do backend
 * não descreve a árvore de pastas do ZIP e o contrato HTTP não a prevê, mas
 * RF-04 exige que o resumo mostre a "estrutura prevista do projeto".
 *
 * A fonte do que está escrito aqui é `docs/architecture/generated-projects.md`.
 * Enquanto o motor de geração não existir (T03) e os templates não existirem
 * (T04), **nada garante que esta árvore corresponda ao ZIP real**. Quando eles
 * existirem, esta projeção precisa ser amarrada ao pacote de verdade — de
 * preferência por um teste que abra um ZIP gerado e compare as duas árvores —
 * ou ser substituída por um campo do próprio catálogo.
 *
 * Regra de contenção: as strings de opção ficam confinadas em
 * {@link PROJECT_STRUCTURE_RULES}. Nenhum template HTML, componente ou CSS
 * deste projeto pode repetir uma delas.
 */

import { Selection } from '../catalog/constraints';
import { OptionValue } from '../catalog/template-options.model';

/** Marcador substituído pelo nome do projeto ao montar a árvore. */
export const PROJECT_TOKEN = '{projeto}';

/** Nome exibido enquanto a pessoa não digitou um nome válido. */
export const PLACEHOLDER_PROJECT_NAME = 'SeuProjeto';

/** Uma condição sobre um campo da seleção. Ausente do `Selection` ⇒ não casa. */
export interface FieldMatch {
  readonly field: string;
  /** O valor precisa estar nesta lista. */
  readonly is?: readonly OptionValue[];
  /** O valor não pode estar nesta lista. */
  readonly isNot?: readonly OptionValue[];
}

/**
 * Uma entrada da árvore. `path` usa `/` como separador e termina em `/` quando
 * é diretório. `when` é uma conjunção: todas as condições precisam casar.
 * Duas regras podem apontar para o mesmo caminho — as notas se somam.
 */
export interface StructureRule {
  readonly path: string;
  readonly note?: string;
  readonly when?: readonly FieldMatch[];
}

export interface StructureNode {
  readonly name: string;
  readonly kind: 'directory' | 'file';
  /** Comentário curto ao lado do nome, ou `null`. */
  readonly note: string | null;
  readonly children: readonly StructureNode[];
}

const P = PROJECT_TOKEN;

/** O mapa dados → árvore. Único ponto do frontend com valores de opção. */
export const PROJECT_STRUCTURE_RULES: readonly StructureRule[] = [
  // ---------------------------------------------------------------- raiz
  { path: `${P}.sln` },
  { path: 'global.json', note: 'fixa a versão do SDK' },
  { path: '.editorconfig' },
  { path: '.gitignore' },
  { path: 'README.md', note: 'os comandos desta combinação' },
  { path: 'requests.http', note: 'exemplos de chamada' },

  // ------------------------------------------------- projeto Api (as duas)
  { path: `src/${P}.Api/${P}.Api.csproj` },
  {
    path: `src/${P}.Api/${P}.Api.csproj`,
    note: 'com Swashbuckle para a interface',
    when: [{ field: 'swagger', is: [true] }],
  },
  { path: `src/${P}.Api/Program.cs`, note: 'composição e endpoints' },
  { path: `src/${P}.Api/appsettings.json` },
  {
    path: `src/${P}.Api/appsettings.json`,
    note: 'cadeia de conexão',
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `src/${P}.Api/appsettings.json`,
    note: 'Authority e Audience do provedor',
    when: [{ field: 'authentication', is: ['jwt'] }],
  },
  { path: `src/${P}.Api/appsettings.Development.json` },
  { path: `src/${P}.Api/Endpoints/HealthEndpoints.cs`, note: 'GET /health, sempre público' },
  { path: `src/${P}.Api/Endpoints/ItemsEndpoints.cs`, note: 'CRUD de Item' },

  // --------------------------------------------------- arquitetura simples
  {
    path: `src/${P}.Api/Models/Item.cs`,
    when: [{ field: 'architecture', is: ['simple'] }],
  },
  {
    path: `src/${P}.Api/Services/ItemService.cs`,
    when: [{ field: 'architecture', is: ['simple'] }],
  },
  {
    path: `src/${P}.Api/Persistence/InMemoryItemStore.cs`,
    note: 'volátil, perdido no reinício',
    when: [
      { field: 'architecture', is: ['simple'] },
      { field: 'database', is: ['none'] },
    ],
  },
  {
    path: `src/${P}.Api/Persistence/AppDbContext.cs`,
    when: [
      { field: 'architecture', is: ['simple'] },
      { field: 'database', isNot: ['none'] },
    ],
  },
  {
    path: `src/${P}.Api/Persistence/Migrations/`,
    note: 'migração inicial do SQLite',
    when: [
      { field: 'architecture', is: ['simple'] },
      { field: 'database', is: ['sqlite'] },
    ],
  },
  {
    path: `src/${P}.Api/Persistence/Migrations/`,
    note: 'migração inicial do PostgreSQL',
    when: [
      { field: 'architecture', is: ['simple'] },
      { field: 'database', is: ['postgresql'] },
    ],
  },
  {
    path: `src/${P}.Api/Persistence/AppUser.cs`,
    note: 'usuários do Identity',
    when: [
      { field: 'architecture', is: ['simple'] },
      { field: 'authentication', is: ['identity'] },
    ],
  },

  // ----------------------------------------------------- clean architecture
  {
    path: `src/${P}.Application/${P}.Application.csproj`,
    note: 'depende só do domínio',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Application/Abstractions/IItemRepository.cs`,
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Application/Items/`,
    note: 'casos de uso',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Domain/${P}.Domain.csproj`,
    note: 'sem referência de projeto',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Domain/Items/Item.cs`,
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Infrastructure/${P}.Infrastructure.csproj`,
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Infrastructure/Persistence/InMemoryItemRepository.cs`,
    note: 'volátil, perdido no reinício',
    when: [
      { field: 'architecture', is: ['clean'] },
      { field: 'database', is: ['none'] },
    ],
  },
  {
    path: `src/${P}.Infrastructure/Persistence/AppDbContext.cs`,
    when: [
      { field: 'architecture', is: ['clean'] },
      { field: 'database', isNot: ['none'] },
    ],
  },
  {
    path: `src/${P}.Infrastructure/Persistence/Migrations/`,
    note: 'migração inicial do SQLite',
    when: [
      { field: 'architecture', is: ['clean'] },
      { field: 'database', is: ['sqlite'] },
    ],
  },
  {
    path: `src/${P}.Infrastructure/Persistence/Migrations/`,
    note: 'migração inicial do PostgreSQL',
    when: [
      { field: 'architecture', is: ['clean'] },
      { field: 'database', is: ['postgresql'] },
    ],
  },
  {
    path: `src/${P}.Infrastructure/Identity/AppUser.cs`,
    note: 'usuários do Identity',
    when: [
      { field: 'architecture', is: ['clean'] },
      { field: 'authentication', is: ['identity'] },
    ],
  },

  // ---------------------------------------------------------------- testes
  { path: `tests/${P}.Tests/${P}.Tests.csproj` },
  { path: `tests/${P}.Tests/HealthEndpointTests.cs` },
  { path: `tests/${P}.Tests/ItemsEndpointTests.cs` },

  // Por último de propósito: diretórios vêm antes de arquivos na árvore, e
  // abrir a listagem por um diretório oculto atrapalharia a leitura.
  { path: '.templategenerator/manifest.json', note: 'versão do template e opções escolhidas' },
];

function matches(condition: FieldMatch, selection: Selection): boolean {
  if (!(condition.field in selection)) {
    return false;
  }
  const value = selection[condition.field];
  if (condition.is && !condition.is.includes(value)) {
    return false;
  }
  if (condition.isNot && condition.isNot.includes(value)) {
    return false;
  }
  return true;
}

function applies(rule: StructureRule, selection: Selection): boolean {
  return (rule.when ?? []).every((condition) => matches(condition, selection));
}

interface MutableNode {
  name: string;
  kind: 'directory' | 'file';
  notes: string[];
  children: Map<string, MutableNode>;
}

function child(parent: MutableNode, name: string, kind: 'directory' | 'file'): MutableNode {
  const existing = parent.children.get(name);
  if (existing) {
    return existing;
  }
  const created: MutableNode = { name, kind, notes: [], children: new Map() };
  parent.children.set(name, created);
  return created;
}

/** Diretórios antes de arquivos; dentro de cada grupo, a ordem das regras. */
function freeze(node: MutableNode): StructureNode {
  const children = [...node.children.values()];
  const directories = children.filter((entry) => entry.kind === 'directory');
  const files = children.filter((entry) => entry.kind === 'file');
  return {
    name: node.name,
    kind: node.kind,
    note: node.notes.length > 0 ? node.notes.join('; ') : null,
    children: [...directories, ...files].map(freeze),
  };
}

/**
 * A árvore prevista para uma seleção, com o nome do projeto aplicado.
 *
 * Um nome vazio ou só com espaços vira {@link PLACEHOLDER_PROJECT_NAME}: a
 * árvore serve para conferir a forma do pacote antes de o nome existir.
 */
export function projectStructure(
  selection: Selection,
  projectName: string,
): readonly StructureNode[] {
  const name = projectName.trim() === '' ? PLACEHOLDER_PROJECT_NAME : projectName.trim();
  const root: MutableNode = { name: '', kind: 'directory', notes: [], children: new Map() };

  for (const rule of PROJECT_STRUCTURE_RULES) {
    if (!applies(rule, selection)) {
      continue;
    }

    const isDirectory = rule.path.endsWith('/');
    const segments = rule.path.replaceAll(PROJECT_TOKEN, name).split('/').filter(Boolean);

    let cursor = root;
    segments.forEach((segment, index) => {
      const last = index === segments.length - 1;
      cursor = child(cursor, segment, last && !isDirectory ? 'file' : 'directory');
    });

    if (rule.note && !cursor.notes.includes(rule.note)) {
      cursor.notes.push(rule.note);
    }
  }

  return freeze(root).children;
}

/** A árvore achatada em linhas, com a profundidade de cada uma. */
export interface StructureLine {
  readonly depth: number;
  readonly node: StructureNode;
  /** Caminho completo, para servir de chave estável na renderização. */
  readonly path: string;
}

export function flattenStructure(
  nodes: readonly StructureNode[],
  depth = 0,
  prefix = '',
): readonly StructureLine[] {
  return nodes.flatMap((node) => {
    const path = `${prefix}/${node.name}`;
    return [{ depth, node, path }, ...flattenStructure(node.children, depth + 1, path)];
  });
}

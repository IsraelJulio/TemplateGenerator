/**
 * ⚠️ PROJEÇÃO — e agora ela está amarrada em tudo que a tela alcança.
 *
 * Este é o **único** lugar do frontend que conhece valores de opção
 * (`clean`, `sqlite`, `identity`, …). Ele existe porque o catálogo do backend
 * não descreve a árvore de pastas do ZIP e o contrato HTTP não a prevê, mas
 * RF-04 exige que o resumo mostre a "estrutura prevista do projeto".
 *
 * A fonte do que está escrito aqui é `docs/architecture/generated-projects.md`.
 * A projeção segue o documento, e não o contrário (ADR-0010).
 *
 * **O que está conferido contra o pacote real:** as duas arquiteturas.
 * `zip-structure.spec.ts` compara caminho a caminho a árvore devolvida por
 * {@link projectStructure} com o conteúdo do ZIP de verdade, reconstruído a
 * cada `dotnet test`; desde T04 o contrato traz também as combinações de
 * `clean`. Foi essa amarração que acusou, em T03, quatro classes de divergência
 * herdadas de T02, e em T04 a projeção inteira da Clean — nomes de arquivo,
 * projeto de testes e a camada em que a porta mora.
 *
 * **Onde a porta de persistência mora**, que é a divergência que mais custou:
 * em Clean ela é `Domain/Abstractions/IItemStore.cs`, não
 * `Application/Abstractions/IItemRepository.cs`. A decisão é de
 * `generated-projects.md`, seção "Onde a porta mora, e por quê" — com a porta em
 * `Application`, `Infrastructure` precisaria de uma aresta que o diagrama não
 * tem. E o nome não é escolha da Clean: `database/<valor>` contribui **um**
 * `ItemStore.cs` para as duas arquiteturas, então o par `IItemStore`/`ItemStore`
 * é o mesmo nos dois lados.
 *
 * **O que entrou em T05:** `sqlite` e `postgresql`. Os fragmentos de banco
 * passaram a existir, os dois valores acenderam, as combinações com banco
 * entraram no contrato gerado e a amarração as alcançou — exatamente como
 * previsto. Cada uma delas traz, além do `AppDbContext.cs` já modelado, o
 * `PersistenceRegistration.cs`, os três arquivos de `Migrations/` e o
 * `.config/dotnet-tools.json` na raiz do ZIP.
 *
 * **O que entrou em T06:** `identity`. O fragmento de autenticação passou a
 * existir, o valor acendeu e as oito combinações dele entraram no contrato
 * gerado — e a amarração cobrou na hora. As duas regras que a projeção
 * mantinha como palpite apontavam para caminhos que o pacote real não tem:
 * `Persistence/AppUser.cs` na Simples e `Infrastructure/Identity/AppUser.cs` na
 * Clean. As duas saíram. No lugar delas entram sete regras sob `Identity/`,
 * **dentro** da pasta de persistência — o usuário, o `DbContext` das tabelas de
 * identidade, o registro dele e os três arquivos da migração própria. Nenhuma
 * condiciona por arquitetura, porque `auth/identity` é um fragmento só e cai
 * onde {@link PERSISTENCE_TOKEN} mandar; foi exatamente o erro do palpite
 * antigo ter escrito uma regra por arquitetura para um arquivo que não varia
 * por ela.
 *
 * **O que continua sendo palpite:** um valor só — `jwt`. Com ADR-0012
 * implementada, a tela o desabilita, de modo que esse palpite é
 * **inalcançável**: nenhuma seleção que o resumo consegue mostrar depende dele.
 * No dia em que o fragmento existir, o valor acende, a combinação entra no
 * contrato gerado e a amarração o alcança sozinha.
 *
 * Regra de contenção: as strings de opção ficam confinadas em
 * {@link PROJECT_STRUCTURE_RULES}, {@link API_PROJECT_NAME_RULES} e
 * {@link PERSISTENCE_DIR_RULES}. Nenhum template HTML, componente ou CSS deste
 * projeto pode repetir uma delas.
 */

import { Selection } from '../catalog/constraints';
import { OptionValue } from '../catalog/template-options.model';

/** Marcador substituído pelo nome do projeto ao montar a árvore. */
export const PROJECT_TOKEN = '{projeto}';

/**
 * Marcador do **projeto Web API** — a pasta e o `.csproj` dele.
 *
 * Não é o mesmo que {@link PROJECT_TOKEN} porque o nome desse projeto é a única
 * coisa da árvore que muda por arquitetura sem que os arquivos mudem junto, e
 * `generated-projects.md` decide os dois casos de formas opostas. Resolver isso
 * num marcador próprio mantém uma entrada por arquivo em
 * {@link PROJECT_STRUCTURE_RULES}; a alternativa seria duplicar nove regras
 * idênticas só para trocar o prefixo.
 */
export const API_PROJECT_TOKEN = '{projeto-api}';

/**
 * Marcador da **pasta de persistência**, que muda de projeto conforme a
 * arquitetura: `Persistence/` dentro do projeto Web API na Simples,
 * `Persistence/` dentro do projeto de Infraestrutura na Clean.
 *
 * Ele existe porque o motor tem o mesmo marcador, `__PersistenceDir__`
 * (`architecture/<valor>/__parts__/PersistenceDir.txt`), e por um motivo que
 * vale para os dois lados: `database/<valor>` contribui **um** arquivo —
 * `ItemStore.cs` — que precisa cair na pasta que a arquitetura escolheu. Escrever
 * duas regras quase iguais aqui, uma por arquitetura, seria reescrever a decisão
 * do motor na tela e deixá-la envelhecer em dobro.
 *
 * O valor dele cita {@link API_PROJECT_TOKEN} e {@link PROJECT_TOKEN}, então é o
 * primeiro a ser expandido.
 */
export const PERSISTENCE_TOKEN = '{persistencia}';

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

/**
 * Um nome que entra no caminho e depende da seleção. A primeira entrada cujo
 * `when` casa vence; se nenhuma casar, o marcador fica sem valor e toda regra
 * que o cite é descartada.
 */
export interface TokenRule {
  readonly value: string;
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
const A = API_PROJECT_TOKEN;

/**
 * Como se chama o projeto Web API, por arquitetura. A decisão é de
 * `generated-projects.md`, seção "Nomes de projeto e de pasta".
 *
 * - **Simples:** o projeto se chama exatamente `<ProjectName>`. **Nada é
 *   concatenado** — não há projeto irmão a desambiguar, então o sufixo não tem
 *   função. É por isso que `Acme.Billing.Api` produz `src/Acme.Billing.Api/`, e
 *   não o `.Api` dobrado que a projeção de T02 mostrava: nada é concatenado,
 *   logo não há o que deduplicar.
 * - **Clean:** `<ProjectName>.Api`, porque ali o sufixo distingue um dos quatro
 *   irmãos. **Decidido em T04:** o sufixo é concatenado sempre, sem comparação e
 *   sem remoção, e `Acme.Billing.Api` produz `src/Acme.Billing.Api.Api/` de
 *   propósito — um desambiguador aplicado só às vezes não desambigua. As quatro
 *   razões estão em `generated-projects.md`, e o nome dobrado está amarrado ao
 *   ZIP real pelo contrato, não só ao documento.
 *
 * Sem `architecture` na seleção nenhuma entrada casa, e aí toda regra que cite
 * {@link API_PROJECT_TOKEN} é descartada — a projeção prefere omitir o projeto
 * a exibir um nome inventado.
 */
export const API_PROJECT_NAME_RULES: readonly TokenRule[] = [
  { value: P, when: [{ field: 'architecture', is: ['simple'] }] },
  { value: `${P}.Api`, when: [{ field: 'architecture', is: ['clean'] }] },
];

/**
 * Onde a pasta de persistência mora, por arquitetura — o lado da tela do
 * `__PersistenceDir__` do motor. Ver {@link PERSISTENCE_TOKEN}.
 *
 * Na Simples o armazenamento fica dentro do próprio projeto Web API; na Clean,
 * dentro do projeto de Infraestrutura, que é quem implementa a porta declarada
 * no Domínio.
 */
export const PERSISTENCE_DIR_RULES: readonly TokenRule[] = [
  { value: `src/${A}/Persistence`, when: [{ field: 'architecture', is: ['simple'] }] },
  {
    value: `src/${P}.Infrastructure/Persistence`,
    when: [{ field: 'architecture', is: ['clean'] }],
  },
];

/** O mapa dados → árvore. Único ponto do frontend com valores de opção. */
export const PROJECT_STRUCTURE_RULES: readonly StructureRule[] = [
  // ---------------------------------------------------------------- raiz
  { path: `${P}.sln` },
  { path: 'global.json', note: 'fixa a versão do SDK' },
  { path: '.editorconfig' },
  { path: '.gitignore' },
  { path: 'README.md', note: 'os comandos desta combinação' },
  { path: 'requests.http', note: 'exemplos de chamada' },
  {
    // Só com banco: fixa o dotnet-ef que aplica as migrações. Fica na raiz do
    // ZIP, fora de qualquer projeto — é ferramenta da solução, não código dela.
    path: '.config/dotnet-tools.json',
    note: 'fixa o dotnet-ef desta solução',
    when: [{ field: 'database', isNot: ['none'] }],
  },

  // ----------------------------------------- projeto Web API (as duas)
  //
  // O que está aqui sai do fragmento de arquitetura nas **duas** arquiteturas,
  // com o mesmo nome de arquivo. Foi a amarração de T04 que mostrou quanto desta
  // lista a projeção de T02 tinha dado por exclusivo da Simples: `ItemEndpoints`,
  // `HealthResponse` e `Properties/launchSettings.json` sempre estiveram nos
  // dois pacotes.
  { path: `src/${A}/${A}.csproj` },
  {
    path: `src/${A}/${A}.csproj`,
    note: 'com Swashbuckle para a interface',
    when: [{ field: 'swagger', is: [true] }],
  },
  { path: `src/${A}/Program.cs`, note: 'composição e endpoints' },
  { path: `src/${A}/appsettings.json` },
  {
    path: `src/${A}/appsettings.json`,
    note: 'cadeia de conexão',
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `src/${A}/appsettings.json`,
    note: 'Authority e Audience do provedor',
    when: [{ field: 'authentication', is: ['jwt'] }],
  },
  { path: `src/${A}/appsettings.Development.json` },
  { path: `src/${A}/Properties/launchSettings.json`, note: 'fixa a porta que o README cita' },
  { path: `src/${A}/Endpoints/HealthEndpoints.cs`, note: 'GET /health, sempre público' },
  { path: `src/${A}/Endpoints/ItemEndpoints.cs`, note: 'CRUD de Item' },
  { path: `src/${A}/Models/HealthResponse.cs` },

  // --------------------------------------------------- arquitetura simples
  //
  // Um projeto só, com o modelo, o serviço e a porta dentro dele. Desde T06
  // **todas** as entradas deste bloco são conferidas contra o ZIP real por
  // `zip-structure.spec.ts`: o que sobrava de projeção aqui eram as de
  // `identity`, e elas saíram deste bloco — o fragmento existe, e o que ele
  // traz não varia por arquitetura, então mora na seção "identidade".
  {
    path: `src/${A}/Models/Item.cs`,
    when: [{ field: 'architecture', is: ['simple'] }],
  },
  {
    path: `src/${A}/Models/ItemInput.cs`,
    note: 'o corpo aceito no POST e no PUT',
    when: [{ field: 'architecture', is: ['simple'] }],
  },
  {
    path: `src/${A}/Services/ItemService.cs`,
    when: [{ field: 'architecture', is: ['simple'] }],
  },
  {
    path: `src/${A}/Persistence/IItemStore.cs`,
    note: 'a porta que o serviço enxerga',
    when: [{ field: 'architecture', is: ['simple'] }],
  },

  // ----------------------------------------------------- clean architecture
  //
  // Quatro projetos, com as dependências do diagrama de `generated-projects.md`.
  // **A porta mora no Domínio** — `Domain/Abstractions/IItemStore.cs` —, e não na
  // Aplicação: com ela na Aplicação, `Infrastructure` precisaria referenciar
  // `Application` para implementá-la, que é a quinta aresta que o diagrama não
  // tem. O nome também não é escolha da Clean: `database/<valor>` contribui o
  // mesmo `ItemStore.cs` para as duas arquiteturas.
  {
    path: `src/${P}.Application/${P}.Application.csproj`,
    note: 'depende só do domínio',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Application/Items/ItemInput.cs`,
    note: 'o corpo aceito no POST e no PUT',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Application/Items/ItemService.cs`,
    note: 'casos de uso',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Domain/${P}.Domain.csproj`,
    note: 'sem referência de projeto',
    when: [{ field: 'architecture', is: ['clean'] }],
  },
  {
    path: `src/${P}.Domain/Abstractions/IItemStore.cs`,
    note: 'a porta que a infraestrutura implementa',
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

  // ------------------------------------------------------------ persistência
  //
  // Uma regra por arquivo, **não** uma por arquitetura: o que muda entre as duas
  // é só a pasta, e quem a resolve é {@link PERSISTENCE_DIR_RULES}. O implemento
  // vem sempre do eixo `database`, e é por isso que ele não está em nenhum dos
  // dois blocos acima.
  { path: `${PERSISTENCE_TOKEN}/ItemStore.cs` },
  {
    path: `${PERSISTENCE_TOKEN}/ItemStore.cs`,
    note: 'volátil, perdido no reinício',
    when: [{ field: 'database', is: ['none'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/ItemStore.cs`,
    note: 'sobre o EF Core',
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/AppDbContext.cs`,
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/PersistenceRegistration.cs`,
    note: 'liga o DbContext e o store ao contêiner',
    when: [{ field: 'database', isNot: ['none'] }],
  },
  // A pasta de migrações e seus três arquivos: a migração inicial, o Designer que
  // a acompanha e o snapshot do modelo. Os nomes são os mesmos nos dois
  // provedores; o que muda é o conteúdo, então a condição é só "tem banco". A
  // nota no diretório é que diz qual provedor — e é ela que `project-structure`
  // confere, porque nota não tem contraparte no ZIP.
  {
    path: `${PERSISTENCE_TOKEN}/Migrations/`,
    note: 'migração inicial do SQLite',
    when: [{ field: 'database', is: ['sqlite'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Migrations/`,
    note: 'migração inicial do PostgreSQL',
    when: [{ field: 'database', is: ['postgresql'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Migrations/20260101000000_InitialCreate.cs`,
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Migrations/20260101000000_InitialCreate.Designer.cs`,
    when: [{ field: 'database', isNot: ['none'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Migrations/AppDbContextModelSnapshot.cs`,
    when: [{ field: 'database', isNot: ['none'] }],
  },

  // -------------------------------------------------------------- identidade
  //
  // Tudo do `auth/identity` cai em `Identity/`, **dentro** da pasta de
  // persistência — o mesmo {@link PERSISTENCE_TOKEN} do `ItemStore.cs`, pelo
  // mesmo motivo: o fragmento de autenticação é **um só** e precisa cair na
  // pasta que a arquitetura escolheu. Por isso nenhuma regra daqui condiciona
  // por `architecture`, e por isso são sete regras, e não catorze.
  //
  // A condição é só `authentication: identity`, sem checar banco: a restrição
  // `identity-requires-database` do catálogo já impede a combinação sem banco
  // de chegar até aqui, e repeti-la nesta lista seria reescrever a regra de
  // compatibilidade na tela.
  //
  // **São dois `DbContext` no mesmo banco** — `AppDbContext`, acima, e
  // `AppIdentityDbContext`, aqui —, cada um com o seu conjunto de migrações. A
  // decisão e o porquê estão em `generated-projects.md`, seção "As tabelas de
  // identidade têm `DbContext` e migração próprios".
  {
    path: `${PERSISTENCE_TOKEN}/Identity/AppUser.cs`,
    note: 'usuários do Identity',
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/AppIdentityDbContext.cs`,
    note: 'as tabelas de identidade, no mesmo banco',
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/IdentityRegistration.cs`,
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/Migrations/`,
    note: 'migração própria das tabelas de identidade',
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/Migrations/20260101000100_IdentitySchema.cs`,
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/Migrations/20260101000100_IdentitySchema.Designer.cs`,
    when: [{ field: 'authentication', is: ['identity'] }],
  },
  {
    path: `${PERSISTENCE_TOKEN}/Identity/Migrations/AppIdentityDbContextModelSnapshot.cs`,
    when: [{ field: 'authentication', is: ['identity'] }],
  },

  // ---------------------------------------------------------------- testes
  //
  // `.Tests` é acrescentado **sempre**, nas duas arquiteturas: aqui o sufixo tem
  // função, porque sem ele o projeto de teste teria o mesmo nome do de produção
  // (`generated-projects.md`). O conteúdo também é o mesmo nos dois pacotes — o
  // serviço exercitado contra um dublê da porta —, e a projeção de T02 que dava
  // à Clean um `HealthEndpointTests`/`ItemsEndpointTests` próprio era palpite.
  { path: `tests/${P}.Tests/${P}.Tests.csproj` },
  { path: `tests/${P}.Tests/FakeItemStore.cs`, note: 'dublê da porta de persistência' },
  { path: `tests/${P}.Tests/ItemServiceTests.cs` },

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

/** Vale para os dois mapas: a condição é a mesma, só o que ela guarda muda. */
function applies(rule: { readonly when?: readonly FieldMatch[] }, selection: Selection): boolean {
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

  // `undefined` quando a seleção não diz a arquitetura: sem ela não há como
  // saber o nome do projeto Web API nem onde fica a persistência, e a projeção
  // omite o que não sabe.
  const apiProject = API_PROJECT_NAME_RULES.find((rule) => applies(rule, selection))?.value;
  const persistenceDir = PERSISTENCE_DIR_RULES.find((rule) => applies(rule, selection))?.value;

  for (const rule of PROJECT_STRUCTURE_RULES) {
    if (!applies(rule, selection)) {
      continue;
    }
    if (apiProject === undefined && rule.path.includes(API_PROJECT_TOKEN)) {
      continue;
    }
    if (persistenceDir === undefined && rule.path.includes(PERSISTENCE_TOKEN)) {
      continue;
    }

    const isDirectory = rule.path.endsWith('/');
    // A pasta de persistência primeiro: o valor dela cita os outros dois
    // marcadores. E `{projeto-api}` antes de `{projeto}`, porque o segundo é
    // prefixo do primeiro e o trocaria pela metade.
    const segments = rule.path
      .replaceAll(PERSISTENCE_TOKEN, persistenceDir ?? '')
      .replaceAll(API_PROJECT_TOKEN, apiProject ?? '')
      .replaceAll(PROJECT_TOKEN, name)
      .split('/')
      .filter(Boolean);

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

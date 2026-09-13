import { readFileSync, readdirSync } from 'node:fs';
import {
  API_PROJECT_NAME_RULES,
  API_PROJECT_TOKEN,
  PLACEHOLDER_PROJECT_NAME,
  PROJECT_STRUCTURE_RULES,
  PROJECT_TOKEN,
  StructureNode,
  flattenStructure,
  projectStructure,
} from './project-structure';

/** Todos os caminhos da árvore, como `a/b/c` (diretório termina em `/`). */
function paths(nodes: readonly StructureNode[], prefix = ''): string[] {
  return nodes.flatMap((node) => {
    const here = `${prefix}${node.name}${node.kind === 'directory' ? '/' : ''}`;
    return [here, ...paths(node.children, here)];
  });
}

function noteOf(nodes: readonly StructureNode[], path: string): string | null {
  const line = flattenStructure(nodes).find(
    (entry) => entry.path.slice(1) === path.replace(/\/$/, ''),
  );
  if (!line) {
    throw new Error(`Caminho ausente da árvore: ${path}`);
  }
  return line.node.note;
}

const SIMPLE = {
  architecture: 'simple',
  database: 'none',
  authentication: 'none',
  swagger: true,
  dotnetVersion: 'net10.0',
};

describe('projeção da estrutura do projeto', () => {
  it('usa o nome digitado em todo lugar onde o marcador aparece', () => {
    const tree = projectStructure(SIMPLE, '  Acme.Billing.Api  ');
    const all = paths(tree);

    expect(all).toContain('Acme.Billing.Api.sln');
    expect(all).toContain('src/Acme.Billing.Api/Acme.Billing.Api.csproj');
    expect(all.join('\n')).not.toContain(PROJECT_TOKEN);
    expect(all.join('\n')).not.toContain(API_PROJECT_TOKEN);
  });

  it('não acrescenta nada ao nome na Simples, e acrescenta `.Api` na Clean', () => {
    // As duas metades da seção "Nomes de projeto e de pasta" de
    // `generated-projects.md`, lado a lado, com o nome que expõe a diferença.
    // Na Simples não há projeto irmão a desambiguar, então o sufixo não existe e
    // o `.Api` dobrado não chega a ser possível — isso está amarrado ao pacote
    // real em `zip-structure.spec.ts`. Na Clean o sufixo distingue um dos quatro
    // irmãos, o nome dobrado sobrevive, e **T04 decide** o que fazer com ele:
    // esta linha registra o comportamento de hoje para que a mudança seja
    // deliberada quando vier, não silenciosa.
    const simples = paths(projectStructure(SIMPLE, 'Acme.Billing.Api'));
    expect(simples).toContain('src/Acme.Billing.Api/');
    expect(simples.some((path) => path.includes('.Api.Api'))).toBe(false);

    const clean = paths(projectStructure({ ...SIMPLE, architecture: 'clean' }, 'Acme.Billing.Api'));
    expect(clean).toContain('src/Acme.Billing.Api.Api/');
  });

  it('cai num nome de exemplo enquanto a pessoa não digitou nenhum', () => {
    expect(paths(projectStructure(SIMPLE, '   '))).toContain(`${PLACEHOLDER_PROJECT_NAME}.sln`);
  });

  it('entrega o conteúdo obrigatório de todo ZIP, em qualquer combinação', () => {
    const obrigatorios = [
      'global.json',
      '.editorconfig',
      '.gitignore',
      'README.md',
      'requests.http',
      '.templategenerator/manifest.json',
      'tests/',
    ];

    for (const architecture of ['simple', 'clean']) {
      for (const database of ['none', 'sqlite', 'postgresql']) {
        const tree = paths(projectStructure({ ...SIMPLE, architecture, database }, 'X'));
        for (const item of obrigatorios) {
          expect(tree.some((path) => path.startsWith(item))).toBe(true);
        }
      }
    }
  });

  it('mostra um projeto só na arquitetura simples e quatro na clean', () => {
    const simples = paths(projectStructure(SIMPLE, 'X')).filter((path) => path.endsWith('.csproj'));
    const clean = paths(projectStructure({ ...SIMPLE, architecture: 'clean' }, 'X')).filter(
      (path) => path.endsWith('.csproj'),
    );

    // Um de código + um de testes; quatro de código + um de testes.
    expect(simples).toHaveLength(2);
    expect(clean).toHaveLength(5);
  });

  it('só acrescenta migrações quando há banco, e diz qual provedor', () => {
    const semBanco = paths(projectStructure(SIMPLE, 'X'));
    expect(semBanco.some((path) => path.includes('Migrations/'))).toBe(false);

    const sqlite = projectStructure({ ...SIMPLE, database: 'sqlite' }, 'X');
    expect(noteOf(sqlite, 'src/X/Persistence/Migrations/')).toContain('SQLite');

    const postgres = projectStructure({ ...SIMPLE, database: 'postgresql' }, 'X');
    expect(noteOf(postgres, 'src/X/Persistence/Migrations/')).toContain('PostgreSQL');
  });

  it('soma as notas quando mais de uma regra aponta para o mesmo arquivo', () => {
    const nota = noteOf(
      projectStructure({ ...SIMPLE, database: 'sqlite', authentication: 'jwt' }, 'X'),
      'src/X/appsettings.json',
    );

    expect(nota).toContain('conexão');
    expect(nota).toContain('Authority');
  });

  it('registra o Swagger no csproj só quando ele está ligado', () => {
    expect(noteOf(projectStructure(SIMPLE, 'X'), 'src/X/X.csproj')).toContain('Swashbuckle');
    expect(
      noteOf(projectStructure({ ...SIMPLE, swagger: false }, 'X'), 'src/X/X.csproj'),
    ).toBeNull();
  });

  it('troca o armazenamento volátil pelo contexto do EF conforme o banco', () => {
    // Caminho inteiro, não só o nome do arquivo: `ItemStore.cs` é sufixo de
    // `IItemStore.cs`, e uma comparação por substring diria "presente" para o
    // par errado.
    const volatil = paths(projectStructure(SIMPLE, 'X'));
    expect(volatil).toContain('src/X/Persistence/IItemStore.cs');
    expect(volatil).toContain('src/X/Persistence/ItemStore.cs');
    expect(volatil).not.toContain('src/X/Persistence/AppDbContext.cs');

    const comBanco = paths(projectStructure({ ...SIMPLE, database: 'sqlite' }, 'X'));
    expect(comBanco).toContain('src/X/Persistence/AppDbContext.cs');
    expect(comBanco).not.toContain('src/X/Persistence/ItemStore.cs');
    expect(comBanco).not.toContain('src/X/Persistence/IItemStore.cs');
  });

  it('ignora uma regra cujo campo a seleção nem carrega', () => {
    // Sem `architecture`, nenhuma regra condicionada a ela entra — e o projeto
    // Web API some junto, porque o nome dele também depende da arquitetura. A
    // projeção omite o que não sabe em vez de exibir um nome inventado.
    const tree = paths(projectStructure({ database: 'none' }, 'X'));
    expect(tree).toContain('X.sln');
    expect(tree.some((path) => path.startsWith('src/'))).toBe(false);
    expect(tree.some((path) => path.includes('.Application/'))).toBe(false);
    expect(tree.some((path) => path.includes('Models/'))).toBe(false);
  });

  it('lista diretórios antes de arquivos, com ordem estável', () => {
    const raiz = projectStructure(SIMPLE, 'X').map((node) => node.kind);
    expect(raiz.indexOf('file')).toBeGreaterThan(raiz.lastIndexOf('directory'));

    const uma = paths(projectStructure(SIMPLE, 'X'));
    const outra = paths(projectStructure(SIMPLE, 'X'));
    expect(uma).toEqual(outra);
  });

  it('achata a árvore preservando a profundidade de cada linha', () => {
    const linhas = flattenStructure(projectStructure(SIMPLE, 'X'));
    const src = linhas.find((linha) => linha.node.name === 'src');
    const api = linhas.find((linha) => linha.node.name === 'X');

    expect(src?.depth).toBe(0);
    expect(api?.depth).toBe(1);
    expect(new Set(linhas.map((linha) => linha.path)).size).toBe(linhas.length);
  });
});

/**
 * O único módulo de produção autorizado a citar valores de opção, relativo à
 * raiz do projeto Angular (`src/web`, que é o diretório de trabalho do Vitest —
 * `contrast.spec.ts` depende do mesmo fato).
 */
const AUTHORIZED_MODULE = 'src/app/core/summary/project-structure.ts';

/**
 * Os valores proibidos, escritos **uma vez só** nesta suíte: as duas direções
 * do teste (tem de estar no módulo autorizado / não pode estar em nenhum outro)
 * leem esta mesma lista. O primeiro teste do bloco amarra a lista às regras de
 * verdade, então ela não pode envelhecer em silêncio.
 *
 * Os valores de `dotnetVersion` ficam de fora porque a projeção não ramifica
 * pela versão do .NET: nenhuma regra os cita, e exigir que estivessem no módulo
 * autorizado seria exigir código morto.
 */
const OPTION_VALUES = [
  'simple',
  'clean',
  'none',
  'sqlite',
  'postgresql',
  'identity',
  'jwt',
] as const;

/**
 * Todo arquivo de **produção** do frontend: `.ts`, `.html` e `.css` sob `src/`,
 * menos os testes, menos o apoio de teste e menos o módulo autorizado.
 */
function productionFiles(directory = 'src', found: string[] = []): string[] {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = `${directory}/${entry.name}`;

    if (entry.isDirectory()) {
      productionFiles(path, found);
      continue;
    }
    if (!/\.(ts|html|css)$/.test(path)) continue;
    if (/\.spec\.ts$/.test(path)) continue; // teste não é produção
    if (path.startsWith('src/app/testing/')) continue; // fixtures e apoio de teste
    if (path === AUTHORIZED_MODULE) continue; // aqui os valores têm de estar

    found.push(path);
  }
  return found;
}

/**
 * Um seletor de atributo que compara com `value`: `[data-value=clean]`,
 * `[data-value="clean"]`, `[data-value='clean']`, e as variantes com operador
 * (`~=`, `*=`, `^=`, `$=`, `|=`), espaço em volta do `=` e modificador
 * (`[data-value="clean" i]`). Em CSS as aspas são opcionais porque os sete
 * valores são identificadores CSS válidos — é justamente por isso que esta
 * forma precisa de regra própria.
 */
function attributeSelector(value: string): RegExp {
  return new RegExp(`=\\s*(["']?)${value.replace(/[^\w-]/g, '\\$&')}\\1(?:\\s+[isIS])?\\s*\\]`);
}

/**
 * Um vazamento é o valor citado numa das **duas formas** em que alguém
 * realmente escreve um valor de opção:
 *
 * 1. **Literal inteiro entre aspas** — `'clean'`, `"clean"`, `` `clean` ``.
 *    Comparação, chave de mapa, atributo de template. Foi assim que o reviewer
 *    de T02 provou o furo (`export const … = ['identity', 'sqlite', 'clean']`).
 * 2. **Seletor de atributo** — `input[data-value=identity]` no CSS,
 *    `querySelector('[data-value=identity]')` no TS. O template já expõe
 *    `[attr.data-value]="option.value"` para que a opção seja selecionável, e
 *    em seletor de atributo as aspas são opcionais: sem esta segunda regra os
 *    sete valores passariam pela porta mais provável de todas — foi o segundo
 *    ataque do reviewer, e passava.
 *
 * O que **não** é vazamento: a palavra solta. `clean` casa com `cleanup` e
 * `none` casa com `list-style: none` e com a palavra inglesa em comentário (os
 * três existem hoje neste repositório). Um teste que acusa `list-style: none` é
 * desligado pela próxima pessoa, e aí não protege mais nada.
 *
 * **O QUE ESCAPA.** Esta lista é o contrário de uma formalidade: o `reviewer`
 * de T03 atacou o reconhecedor com formas que ninguém tinha tentado e **todas
 * as cinco abaixo passaram**. Leia antes de confiar nesta cerca.
 *
 * - **Valor colado a um prefixo ou sufixo, sem aspas próprias** — e este é o
 *   furo que mais importa:
 *
 *   ```
 *   .option--clean { … }              classe CSS por valor
 *   #opt-postgresql { … }             id CSS por valor
 *   class="badge badge-identity"      classe estática no HTML
 *   [class.arch-simple]="…"           binding de classe do Angular
 *   ```
 *
 *   `[class.arch-simple]` é **a forma idiomática de estilizar por opção num
 *   template Angular**. Ou seja: a cerca não cobre o caso que ADR-0010 diz
 *   existir para conter — *"alguém estilizar ou ramificar uma opção específica
 *   sem pensar"*. Quem for estilizar por opção não será impedido aqui.
 * - **Valor dentro de uma string maior** — `'simple/sqlite/identity'`,
 *   `` `arch-${…}` ``. A forma 1 exige a aspa colada dos dois lados.
 * - **Valor montado em tempo de execução** — `'cle' + 'an'`, `` `${p}ql` ``,
 *   `atob(…)`, escape CSS (`\63 lean`). Nenhuma varredura de texto alcança.
 * - **Identificador solto** — `const ICON = { identity: '…' }`. Deixado de fora
 *   *de propósito*: `identity: (x) => x` é código inocente com exatamente a
 *   mesma forma, e a ambiguidade está na linguagem, não na técnica — um
 *   analisador de sintaxe erraria igual.
 * - **Arquivo de extensão não varrida.** `productionFiles()` filtra
 *   `.ts|.html|.css`. Um `.json`, `.scss` ou `.svg` sob `src/` não é lido —
 *   e já existe um: `zip-structure.contract.json`, que **precisa** conter
 *   valores de opção porque é gerado a partir do ZIP. Estender a varredura
 *   exige uma lista de autorizados, não só mais uma extensão no filtro.
 *
 * **A garantia, dita sem folga:** dentro de `.ts`, `.html` e `.css`, um valor de
 * opção **escrito como literal inteiro entre aspas ou como seletor de atributo**
 * não passa. Só isso. **Não** é "nenhuma forma legível passa" — a lista acima
 * são cinco formas legíveis que passam, uma delas idiomática. É uma cerca
 * contra o descuido, não um muro contra a intenção, e ADR-0010 registra o mesmo
 * limite do lado da arquitetura. Quem acrescentar uma forma nova de escrever
 * valor no frontend precisa **reatacar este reconhecedor antes de confiar
 * nele** — foi assim que os quatro furos conhecidos apareceram, nenhum deles
 * por leitura.
 */
function leaksValue(source: string, value: string): boolean {
  const quoted = ["'", '"', '`'].some((quote) => source.includes(`${quote}${value}${quote}`));
  return quoted || attributeSelector(value).test(source);
}

describe('contenção dos valores de opção', () => {
  it('a lista de valores proibidos é a que as regras realmente usam', () => {
    const usados = new Set<string>();
    // Os **dois** mapas do módulo, não só o da árvore: quando o nome do projeto
    // Web API virou um marcador próprio, `simple` e `clean` passaram a aparecer
    // também em `API_PROJECT_NAME_RULES`. Varrer só um dos dois deixaria a porta
    // aberta para as regras migrarem para o mapa não varrido.
    for (const rule of [...PROJECT_STRUCTURE_RULES, ...API_PROJECT_NAME_RULES]) {
      for (const condition of rule.when ?? []) {
        for (const value of [...(condition.is ?? []), ...(condition.isNot ?? [])]) {
          if (typeof value === 'string') {
            usados.add(value);
          }
        }
      }
    }

    // Some um valor das regras e este teste cai: a lista acima não vira ficção.
    expect([...usados].sort()).toEqual([...OPTION_VALUES].sort());
  });

  it('sabe distinguir um vazamento de uma palavra inocente', () => {
    // A decisão documentada em `leaksValue`, executável.

    // Forma 1: literal entre aspas.
    expect(leaksValue("const REVIEWER_LEAK = ['clean'];", 'clean')).toBe(true);
    expect(leaksValue('[value]="\'sqlite\'"', 'sqlite')).toBe(true);

    // Forma 2: seletor de atributo — com aspas e sem, no CSS e no TS.
    expect(leaksValue('.option:has(input[data-value=identity]) { color: red; }', 'identity')).toBe(
      true,
    );
    expect(leaksValue("querySelector('[data-value=identity]')", 'identity')).toBe(true);
    expect(leaksValue('.option[data-value="postgresql" i] {}', 'postgresql')).toBe(true);
    expect(leaksValue('li[class~=none] {}', 'none')).toBe(true);

    // Palavra inocente: `: none` nunca é `=none]`, e `clean` dentro de `cleanup`
    // não é o valor.
    expect(leaksValue('list-style: none;', 'none')).toBe(false);
    expect(leaksValue('border-bottom: none;', 'none')).toBe(false);
    expect(leaksValue('function cleanup() {}', 'clean')).toBe(false);
    expect(leaksValue('input[id="${PROJECT_NAME}"]', 'none')).toBe(false);

    // Fora do alcance, e assumido como tal em `leaksValue`: valor montado em
    // tempo de execução e identificador solto.
    expect(leaksValue("const a = 'cle' + 'an';", 'clean')).toBe(false);
    expect(leaksValue('const ICON = { identity: shield };', 'identity')).toBe(false);
  });

  it('o módulo da projeção cita cada valor de opção', () => {
    const source = readFileSync(AUTHORIZED_MODULE, 'utf8');

    for (const value of OPTION_VALUES) {
      expect(leaksValue(source, value), `${value} sumiu de ${AUTHORIZED_MODULE}`).toBe(true);
    }
  });

  it('é o único módulo do frontend que cita valores de opção', () => {
    // RF-02: fora deste módulo, nenhum código de produção da tela conhece os
    // valores — eles vêm do catálogo. Aqui a varredura é do repositório de
    // verdade, não do arquivo autorizado.
    const files = productionFiles();

    // Se a varredura deixar de enxergar os arquivos, o teste passaria à toa.
    expect(files).toContain('src/app/features/configurator/configurator.ts');
    expect(files).toContain('src/app/features/configurator/configurator.html');
    expect(files).toContain('src/styles.css');
    expect(files.length).toBeGreaterThan(15);

    const vazamentos = files.flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      return OPTION_VALUES.filter((value) => leaksValue(source, value)).map(
        (value) => `${file}: '${value}'`,
      );
    });

    expect(vazamentos).toEqual([]);
  });

  it('nenhuma regra aponta para um caminho vazio', () => {
    for (const rule of PROJECT_STRUCTURE_RULES) {
      expect(
        rule.path
          .replaceAll(API_PROJECT_TOKEN, 'X')
          .replaceAll(PROJECT_TOKEN, 'X')
          .split('/')
          .filter(Boolean).length,
      ).toBeGreaterThan(0);
    }
  });

  it('todo marcador citado nas regras tem quem o resolva', () => {
    // Um marcador sem mapa faria a árvore exibir `{projeto-api}` na tela, e a
    // amarração ao ZIP só acusaria isso na combinação que a usa.
    const resolviveis = [PROJECT_TOKEN, API_PROJECT_TOKEN];
    const citados = new Set(
      PROJECT_STRUCTURE_RULES.flatMap((rule) => rule.path.match(/\{[^}]+\}/g) ?? []),
    );

    expect([...citados].sort()).toEqual([...resolviveis].sort());
    expect(API_PROJECT_NAME_RULES.length).toBeGreaterThan(0);
  });

  it('toda condição declara `is` ou `isNot`', () => {
    for (const rule of [...PROJECT_STRUCTURE_RULES, ...API_PROJECT_NAME_RULES]) {
      for (const condition of rule.when ?? []) {
        expect(condition.is !== undefined || condition.isNot !== undefined).toBe(true);
        expect(condition.field.length).toBeGreaterThan(0);
      }
    }
  });
});

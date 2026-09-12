import { readFileSync, readdirSync } from 'node:fs';
import {
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
    expect(all).toContain('src/Acme.Billing.Api.Api/Acme.Billing.Api.Api.csproj');
    expect(all.join('\n')).not.toContain(PROJECT_TOKEN);
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
    expect(noteOf(sqlite, 'src/X.Api/Persistence/Migrations/')).toContain('SQLite');

    const postgres = projectStructure({ ...SIMPLE, database: 'postgresql' }, 'X');
    expect(noteOf(postgres, 'src/X.Api/Persistence/Migrations/')).toContain('PostgreSQL');
  });

  it('soma as notas quando mais de uma regra aponta para o mesmo arquivo', () => {
    const nota = noteOf(
      projectStructure({ ...SIMPLE, database: 'sqlite', authentication: 'jwt' }, 'X'),
      'src/X.Api/appsettings.json',
    );

    expect(nota).toContain('conexão');
    expect(nota).toContain('Authority');
  });

  it('registra o Swagger no csproj só quando ele está ligado', () => {
    expect(noteOf(projectStructure(SIMPLE, 'X'), 'src/X.Api/X.Api.csproj')).toContain(
      'Swashbuckle',
    );
    expect(
      noteOf(projectStructure({ ...SIMPLE, swagger: false }, 'X'), 'src/X.Api/X.Api.csproj'),
    ).toBeNull();
  });

  it('troca o armazenamento volátil pelo contexto do EF conforme o banco', () => {
    const volatil = paths(projectStructure(SIMPLE, 'X')).join('\n');
    expect(volatil).toContain('InMemoryItemStore.cs');
    expect(volatil).not.toContain('AppDbContext.cs');

    const comBanco = paths(projectStructure({ ...SIMPLE, database: 'sqlite' }, 'X')).join('\n');
    expect(comBanco).toContain('AppDbContext.cs');
    expect(comBanco).not.toContain('InMemoryItemStore.cs');
  });

  it('ignora uma regra cujo campo a seleção nem carrega', () => {
    // Sem `architecture`, nenhuma regra condicionada a ela entra.
    const tree = paths(projectStructure({ database: 'none' }, 'X'));
    expect(tree).toContain('X.sln');
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
    const api = linhas.find((linha) => linha.node.name === 'X.Api');

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
 * **O que fica fora do alcance de um reconhecedor textual**, dito aqui para que
 * ninguém leia este teste como prova:
 *
 * - **Valor montado em tempo de execução** — `'cle' + 'an'`, `` `${p}ql` ``,
 *   `atob(…)`, escape CSS (`\63 lean`). Nenhuma varredura de texto alcança.
 * - **Identificador solto** — `const ICON = { identity: '…' }`. Deixado de fora
 *   *de propósito*: `identity: (x) => x` é código inocente com exatamente a
 *   mesma forma, e a ambiguidade está na linguagem, não na técnica — um
 *   analisador de sintaxe erraria igual.
 *
 * A garantia, então, é estreita e honesta: **nenhuma forma legível de citar um
 * valor de opção passa**. Não é "é impossível vazar". ADR-0010 registra o mesmo
 * limite do lado da arquitetura.
 */
function leaksValue(source: string, value: string): boolean {
  const quoted = ["'", '"', '`'].some((quote) => source.includes(`${quote}${value}${quote}`));
  return quoted || attributeSelector(value).test(source);
}

describe('contenção dos valores de opção', () => {
  it('a lista de valores proibidos é a que as regras realmente usam', () => {
    const usados = new Set<string>();
    for (const rule of PROJECT_STRUCTURE_RULES) {
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
        rule.path.replaceAll(PROJECT_TOKEN, 'X').split('/').filter(Boolean).length,
      ).toBeGreaterThan(0);
    }
  });

  it('toda condição declara `is` ou `isNot`', () => {
    for (const rule of PROJECT_STRUCTURE_RULES) {
      for (const condition of rule.when ?? []) {
        expect(condition.is !== undefined || condition.isNot !== undefined).toBe(true);
        expect(condition.field.length).toBeGreaterThan(0);
      }
    }
  });
});

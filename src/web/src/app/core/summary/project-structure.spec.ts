import { readFileSync, readdirSync } from 'node:fs';
import {
  API_PROJECT_NAME_RULES,
  API_PROJECT_TOKEN,
  PERSISTENCE_DIR_RULES,
  PERSISTENCE_TOKEN,
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

  it('mantém a porta e o implemento, e acrescenta o contexto do EF com banco', () => {
    // Caminho inteiro, não só o nome do arquivo: `ItemStore.cs` é sufixo de
    // `IItemStore.cs`, e uma comparação por substring diria "presente" para o
    // par errado.
    //
    // **Corrigido em T04**, e não é ajuste de teste a gosto: até T03 esta linha
    // afirmava que o banco *substituía* `ItemStore.cs` por `AppDbContext.cs`.
    // O par `IItemStore`/`ItemStore` vem do fragmento de arquitetura e do eixo
    // `database` — `database/<valor>` contribui **um** `ItemStore.cs` —, então
    // ele não some quando o banco muda: o que muda é o que está dentro dele, e o
    // contexto do EF entra ao lado.
    const volatil = paths(projectStructure(SIMPLE, 'X'));
    expect(volatil).toContain('src/X/Persistence/IItemStore.cs');
    expect(volatil).toContain('src/X/Persistence/ItemStore.cs');
    expect(volatil).not.toContain('src/X/Persistence/AppDbContext.cs');

    const comBanco = paths(projectStructure({ ...SIMPLE, database: 'sqlite' }, 'X'));
    expect(comBanco).toContain('src/X/Persistence/AppDbContext.cs');
    expect(comBanco).toContain('src/X/Persistence/IItemStore.cs');
    expect(comBanco).toContain('src/X/Persistence/ItemStore.cs');
  });

  it('põe a persistência no projeto que a arquitetura escolhe', () => {
    // O lado da tela do `__PersistenceDir__` do motor: o mesmo `ItemStore.cs`,
    // em projetos diferentes. Na Clean a porta fica no Domínio, porque com ela
    // na Aplicação a Infraestrutura precisaria de uma aresta que o diagrama de
    // `generated-projects.md` não tem.
    const clean = paths(projectStructure({ ...SIMPLE, architecture: 'clean' }, 'X'));

    expect(clean).toContain('src/X.Infrastructure/Persistence/ItemStore.cs');
    expect(clean).toContain('src/X.Domain/Abstractions/IItemStore.cs');
    expect(clean.some((path) => path.includes('.Application/Abstractions/'))).toBe(false);
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
 * O arquivo de **dados** autorizado a citar valores de opção. Ele é gerado a
 * partir do ZIP real por `ZipStructureContractTests.cs`, e conter os valores é
 * a razão de ele existir — não um vazamento.
 *
 * A lista existe porque ampliar a varredura para `.json` sem ela derrubaria a
 * suíte no primeiro arquivo legítimo, e a correção óbvia seria estreitar a
 * varredura de novo.
 */
const AUTHORIZED_DATA = ['src/app/core/summary/zip-structure.contract.json'];

/**
 * Todo arquivo de **produção** do frontend sob `src/`, menos os testes, menos o
 * apoio de teste, menos o módulo autorizado e menos os dados autorizados.
 *
 * **Extensões varridas:** `.ts`, `.html`, `.css`, `.scss` e `.json`. As duas
 * últimas entraram em T04: até então a varredura lia `.ts|.html|.css` e um
 * `.json`, `.scss` ou `.svg` sob `src/` escapava **inteiro**, com qualquer
 * conteúdo — foi o quinto furo que o `reviewer` de T03 encontrou.
 *
 * **`.svg` continua de fora, e isto é buraco declarado, não cobertura:**
 * `fill="none"` é a forma idiomática de dizer "sem preenchimento" num ícone e
 * casa com a forma 1 do reconhecedor. Varrer `.svg` faria a cerca acusar o
 * primeiro ícone que alguém acrescentasse, e uma cerca que grita sem motivo é
 * desligada pela próxima pessoa — que é o custo que ADR-0010 manda pesar. Quem
 * puser um `.svg` em `src/` precisa saber que ele não é lido.
 */
function productionFiles(directory = 'src', found: string[] = []): string[] {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = `${directory}/${entry.name}`;

    if (entry.isDirectory()) {
      productionFiles(path, found);
      continue;
    }
    if (!/\.(ts|html|css|scss|json)$/.test(path)) continue;
    if (/\.spec\.ts$/.test(path)) continue; // teste não é produção
    if (path.startsWith('src/app/testing/')) continue; // fixtures e apoio de teste
    if (path === AUTHORIZED_MODULE) continue; // aqui os valores têm de estar
    if (AUTHORIZED_DATA.includes(path)) continue; // gerado do ZIP, cita por dever

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

/** Caractere que forma palavra. Vizinho dele, o valor é parte de outra palavra. */
const WORD = /[A-Za-z0-9]/;

/** Caractere que **junta** dois segmentos num token só: `a-b`, `a_b`, `a.b`, `a/b`. */
const GLUE = /[-._/]/;

/**
 * A partir de `from`, andando em `step`, atravessa uma sequência de separadores
 * e diz se há **outro segmento** do outro lado.
 *
 * A sequência importa: em `.option--clean` o vizinho imediato é `-` e o
 * seguinte também. Parar no primeiro separador deixaria passar o hífen duplo,
 * que é a convenção BEM e portanto a forma mais provável de todas.
 */
function joinsAnotherSegment(source: string, from: number, step: number): boolean {
  let at = from;
  let crossed = false;

  while (at >= 0 && at < source.length && GLUE.test(source[at])) {
    crossed = true;
    at += step;
  }

  return crossed && at >= 0 && at < source.length && WORD.test(source[at]);
}

/**
 * O valor **colado a um prefixo ou sufixo** dentro de um token composto:
 * `.option--clean`, `#opt-postgresql`, `badge-identity`, `[class.arch-simple]`,
 * `'simple/sqlite/identity'`.
 *
 * A regra tem duas metades, e as duas são necessárias:
 *
 * 1. o vizinho imediato **não** pode formar palavra — senão `clean` acusaria
 *    `cleanup`, e o valor nem está ali;
 * 2. de pelo menos um lado, atravessando os separadores, precisa haver **outro
 *    segmento** — senão `list-style: none` e a palavra inglesa solta em
 *    comentário virariam vazamento.
 *
 * É a segunda metade que separa o token composto da prosa, e é ela que faz esta
 * forma valer a pena: `: none;` não tem segmento vizinho colado, `badge-identity`
 * tem.
 */
function gluedToToken(source: string, value: string): boolean {
  for (let at = source.indexOf(value); at !== -1; at = source.indexOf(value, at + 1)) {
    const before = source[at - 1];
    const after = source[at + value.length];

    if ((before !== undefined && WORD.test(before)) || (after !== undefined && WORD.test(after))) {
      continue;
    }
    if (
      joinsAnotherSegment(source, at - 1, -1) ||
      joinsAnotherSegment(source, at + value.length, 1)
    ) {
      return true;
    }
  }

  return false;
}

/**
 * Um vazamento é o valor citado numa das **três formas** em que alguém realmente
 * escreve um valor de opção:
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
 * 3. **Colado a prefixo ou sufixo** — `.option--clean`, `#opt-postgresql`,
 *    `class="badge badge-identity"`, `[class.arch-simple]="…"` e o valor dentro
 *    de string maior, `'simple/sqlite/identity'`. **Acrescentada em T04**, e era
 *    a mais grave das que faltavam: `[class.arch-simple]` é *a* forma idiomática
 *    de estilizar por opção num template Angular, ou seja, a cerca não cobria o
 *    caso exato que ADR-0010 diz existir para conter. Ver {@link gluedToToken}.
 *
 * O que **não** é vazamento: a palavra solta. `clean` casa com `cleanup` e
 * `none` casa com `list-style: none` e com a palavra inglesa em comentário (os
 * três existem hoje neste repositório). Um teste que acusa `list-style: none` é
 * desligado pela próxima pessoa, e aí não protege mais nada.
 *
 * **O QUE AINDA ESCAPA**, e esta lista é o contrário de uma formalidade — todas
 * as formas conhecidas apareceram por reataque, nenhuma por leitura:
 *
 * - **Valor montado em tempo de execução** — `'cle' + 'an'`, `` `${p}ql` ``,
 *   `atob(…)`, escape CSS (`3 lean`). Nenhuma varredura de texto alcança, e
 *   nenhuma alcançará.
 * - **Identificador solto** — `const ICON = { identity: '…' }`. Deixado de fora
 *   *de propósito*: `identity: (x) => x` é código inocente com exatamente a
 *   mesma forma, e a ambiguidade está na linguagem, não na técnica — um
 *   analisador de sintaxe erraria igual.
 * - **`.svg` sob `src/`**, que {@link productionFiles} não lê, pela razão
 *   escrita lá.
 *
 * **A garantia, dita sem folga:** dentro de `.ts`, `.html`, `.css`, `.scss` e
 * `.json` não autorizado, um valor de opção **escrito como literal entre aspas,
 * como seletor de atributo ou colado a outro segmento de um token** não passa.
 * Só isso. É uma cerca contra o descuido, não um muro contra a intenção, e
 * ADR-0010 registra o mesmo limite do lado da arquitetura. Quem acrescentar uma
 * forma nova de escrever valor no frontend precisa **reatacar este reconhecedor
 * antes de confiar nele** — foi assim que os furos conhecidos apareceram.
 */
function leaksValue(source: string, value: string): boolean {
  const quoted = ["'", '"', '`'].some((quote) => source.includes(`${quote}${value}${quote}`));
  return quoted || attributeSelector(value).test(source) || gluedToToken(source, value);
}

describe('contenção dos valores de opção', () => {
  it('a lista de valores proibidos é a que as regras realmente usam', () => {
    const usados = new Set<string>();
    // Os **três** mapas do módulo, não só o da árvore: quando o nome do projeto
    // Web API virou um marcador próprio, `simple` e `clean` passaram a aparecer
    // também em `API_PROJECT_NAME_RULES`, e em T04 o mesmo aconteceu com
    // `PERSISTENCE_DIR_RULES`. Varrer só um deles deixaria a porta aberta para
    // as regras migrarem para o mapa não varrido.
    for (const rule of [
      ...PROJECT_STRUCTURE_RULES,
      ...API_PROJECT_NAME_RULES,
      ...PERSISTENCE_DIR_RULES,
    ]) {
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

    // Forma 3: colado a prefixo ou sufixo. São **as cinco formas** com que o
    // `reviewer` de T03 furou a cerca, uma a uma, na ordem em que ele as
    // reportou. A quarta é a que mais importa: é como se estiliza por opção num
    // template Angular.
    expect(leaksValue('.option--clean { border: 0; }', 'clean')).toBe(true);
    expect(leaksValue('#opt-postgresql { border: 0; }', 'postgresql')).toBe(true);
    expect(leaksValue('<span class="badge badge-identity"></span>', 'identity')).toBe(true);
    expect(leaksValue('<div [class.arch-simple]="isArch()"></div>', 'simple')).toBe(true);
    expect(leaksValue("const KEY = 'simple/sqlite/identity';", 'sqlite')).toBe(true);

    // E as variantes vizinhas das mesmas, que uma regra de "um separador só"
    // deixaria escapar.
    expect(leaksValue('.opt_clean {}', 'clean')).toBe(true);
    expect(leaksValue('[class.x--none] {}', 'none')).toBe(true);
    expect(leaksValue('<i class="icon-jwt"></i>', 'jwt')).toBe(true);

    // Palavra inocente: `: none` nunca é `=none]`, `clean` dentro de `cleanup`
    // não é o valor, e a palavra solta em prosa não tem segmento colado.
    expect(leaksValue('list-style: none;', 'none')).toBe(false);
    expect(leaksValue('border-bottom: none;', 'none')).toBe(false);
    expect(leaksValue('function cleanup() {}', 'clean')).toBe(false);
    expect(leaksValue('input[id="${PROJECT_NAME}"]', 'none')).toBe(false);
    expect(leaksValue('/* nenhum banco: none, sem persistência. */', 'none')).toBe(false);
    expect(leaksValue('.tree__line--directory { display: none; }', 'none')).toBe(false);

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

  it('a lista de dados autorizados existe e está segurando alguma coisa', () => {
    // Duas maneiras de a lista virar decoração: apontar para um arquivo que não
    // existe mais, e apontar para um que não citaria valor nenhum. Nos dois
    // casos ela passaria a esconder um furo futuro em vez de uma citação
    // legítima, e ninguém perceberia.
    for (const path of AUTHORIZED_DATA) {
      const source = readFileSync(path, 'utf8');
      expect(
        OPTION_VALUES.some((value) => leaksValue(source, value)),
        `${path} não cita valor de opção — então não precisa de autorização`,
      ).toBe(true);
    }
  });

  it('nenhuma regra aponta para um caminho vazio', () => {
    for (const rule of PROJECT_STRUCTURE_RULES) {
      expect(
        rule.path
          .replaceAll(PERSISTENCE_TOKEN, 'X/Y')
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
    const resolviveis = [PROJECT_TOKEN, API_PROJECT_TOKEN, PERSISTENCE_TOKEN];
    const citados = new Set(
      PROJECT_STRUCTURE_RULES.flatMap((rule) => rule.path.match(/\{[^}]+\}/g) ?? []),
    );

    expect([...citados].sort()).toEqual([...resolviveis].sort());
    expect(API_PROJECT_NAME_RULES.length).toBeGreaterThan(0);
  });

  it('toda condição declara `is` ou `isNot`', () => {
    for (const rule of [
      ...PROJECT_STRUCTURE_RULES,
      ...API_PROJECT_NAME_RULES,
      ...PERSISTENCE_DIR_RULES,
    ]) {
      for (const condition of rule.when ?? []) {
        expect(condition.is !== undefined || condition.isNot !== undefined).toBe(true);
        expect(condition.field.length).toBeGreaterThan(0);
      }
    }
  });
});

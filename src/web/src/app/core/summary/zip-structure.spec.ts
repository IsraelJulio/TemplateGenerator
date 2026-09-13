import { readFileSync } from 'node:fs';
import { Selection } from '../catalog/constraints';
import { StructureNode, projectStructure } from './project-structure';

/**
 * O lado **frontend** da amarração do critério de aceite 12 de T03 e de
 * ADR-0010 ("Quando esta decisão expira", saída 1): a árvore que a tela mostra
 * como "estrutura prevista" é conferida contra o conteúdo **real** do ZIP.
 *
 * O contrato lido aqui não é escrito à mão. Ele é reconstruído do pacote gerado
 * a cada execução de `dotnet test`, por
 * `tests/TemplateGenerator.Matrix.Tests/Layer1/ZipStructureContractTests.cs`,
 * que falha se o arquivo divergir do ZIP. Os dois testes juntos fecham o
 * circuito:
 *
 * - o de C# garante que o contrato **é** o ZIP;
 * - este garante que a projeção **é** o contrato.
 *
 * Nenhum dos dois sozinho bastaria, e é por isso que são dois. Reimplementar
 * `projectStructure()` em C# para fazer tudo de um lado só seria conferir uma
 * cópia da derivação — o teste passaria com a tela mostrando outra coisa, que é
 * exatamente a falha que ADR-0010 nomeia, e a mesma que em T01 deixou a fixture
 * do catálogo divergir do payload real em três pontos sem quebrar teste algum.
 *
 * **O que é comparado:** o conjunto de caminhos, com `/` no fim quando é
 * diretório, por **igualdade** — não por contenção. Uma projeção que listasse
 * metade da árvore passaria em qualquer teste de subconjunto.
 *
 * **O que não é comparado, dito com todas as letras:** as notas ("volátil,
 * perdido no reinício") e a ordem de exibição. Nota é prosa para humano e não
 * tem contraparte no pacote; a ordem é assunto da tela e já tem teste próprio em
 * `project-structure.spec.ts`.
 *
 * **O que entrou em T04:** a arquitetura Clean. O contrato passou de quatro para
 * oito combinações, e a comparação acusou de uma vez a projeção inteira da
 * Clean, herdada de T02 — `ItemsEndpoints.cs` por `ItemEndpoints.cs`, um projeto
 * de testes inventado e a porta de persistência na camada errada. Nenhuma delas
 * tinha derrubado um teste antes.
 *
 * **O que continua fora, e não é omissão:** as combinações com banco ou com
 * autenticação, cujos fragmentos ainda não existem. A amarração cobre
 * combinação, não eixo — e a lista de combinações amarradas é derivada da
 * disponibilidade, então cada fragmento novo entra aqui sozinho. Como a tela
 * desabilita todo valor sem template (ADR-0012), tudo que a pessoa consegue
 * selecionar está nesta lista.
 */

interface ContractEntry {
  readonly selecao: Record<string, string | boolean>;
  readonly projectName: string;
  readonly caminhos: readonly string[];
}

interface Contract {
  readonly $origem: string;
  readonly combinacoes: readonly ContractEntry[];
}

/** Relativo a `src/web`, que é o diretório de trabalho do Vitest. */
const CONTRACT_FILE = 'src/app/core/summary/zip-structure.contract.json';

const contract: Contract = JSON.parse(readFileSync(CONTRACT_FILE, 'utf8'));

/** Todos os caminhos da árvore, como `a/b/` para diretório e `a/b/c` para arquivo. */
function paths(nodes: readonly StructureNode[], prefix = ''): string[] {
  return nodes.flatMap((node) => {
    const here = `${prefix}${node.name}${node.kind === 'directory' ? '/' : ''}`;
    return [here, ...paths(node.children, here)];
  });
}

describe('a projeção conferida contra o ZIP real', () => {
  it('o contrato existe, não está vazio e traz a raiz da árvore', () => {
    // Guarda contra amarração vazia, pelo mesmo motivo do assert de sanidade de
    // ADR-0008 e ADR-0011: um contrato sem combinações faria o `for` abaixo não
    // executar nenhuma comparação, e a suíte ficaria verde sem verificar nada.
    expect(contract.combinacoes.length).toBeGreaterThan(0);

    for (const entry of contract.combinacoes) {
      expect(entry.caminhos.length).toBeGreaterThan(20);
      expect(entry.caminhos).toContain('.templategenerator/manifest.json');
      expect(entry.caminhos).toContain('README.md');
      expect(entry.projectName.length).toBeGreaterThan(0);
    }
  });

  for (const entry of contract.combinacoes) {
    const rotulo =
      `${entry.selecao['architecture']}/${entry.selecao['database']}/` +
      `${entry.selecao['authentication']}/swagger=${entry.selecao['swagger']}` +
      ` — ${entry.projectName}`;

    it(`bate com o pacote de ${rotulo}`, () => {
      const previsto = [...paths(projectStructure(entry.selecao as Selection, entry.projectName))]
        .slice()
        .sort();
      const real = [...entry.caminhos].slice().sort();

      const sobrando = previsto.filter((path) => !real.includes(path));
      const faltando = real.filter((path) => !previsto.includes(path));

      expect(
        { sobrando, faltando },
        `A "estrutura prevista" divergiu do ZIP de ${rotulo}.\n` +
          `  A tela mostra e o pacote não tem: ${sobrando.join(', ') || '(nada)'}\n` +
          `  O pacote tem e a tela não mostra: ${faltando.join(', ') || '(nada)'}\n` +
          'A fonte é docs/architecture/generated-projects.md; a projeção a segue, e não o ' +
          'contrário (ADR-0010).',
      ).toEqual({ sobrando: [], faltando: [] });
    });
  }

  it('o nome do projeto entra na árvore sem nada concatenado', () => {
    // A decisão de generated-projects.md para a arquitetura Simples, do lado da
    // tela: `Acme.Billing.Api` produz `src/Acme.Billing.Api/`, nunca
    // `src/Acme.Billing.Api.Api/`. O contrato já carrega essa combinação; aqui o
    // ponto fica dito por escrito, para não depender de alguém reconhecê-lo no
    // meio de uma lista de caminhos.
    const entry = contract.combinacoes.find((item) => item.projectName.endsWith('.Api'));

    expect(entry, 'o contrato precisa amarrar um nome terminado em .Api').toBeDefined();

    const previsto = paths(projectStructure(entry!.selecao as Selection, entry!.projectName));

    expect(previsto).toContain(`src/${entry!.projectName}/`);
    expect(previsto.some((path) => path.includes('.Api.Api'))).toBe(false);
  });
});

# Disciplina de contexto

Como um agente deste projeto decide **o que carregar e o que não carregar**. Vale para Claude Code
e para Codex; nenhuma regra aqui depende de mecanismo de uma ferramenta só.

A razão de existir está em [ADR-0014](decisions/adr-0014-divulgacao-progressiva-de-contexto.md):
contexto não é armazenamento, é orçamento de atenção. Uma sessão que carregou 200 KB de documento
meio-relevante **raciocina pior** que uma que carregou 20 KB do que importa — antes de ser um
problema de custo, é um problema de correção.

## 1. Divulgação progressiva

Carregue na ordem, e pare assim que tiver o suficiente:

```
CLAUDE.md / AGENTS.md   → invariantes e roteador
docs/backlog.json       → estado
tarefa corrente         → objetivo, roles, context[], verifications
context[] da tarefa     → só os documentos que a tarefa declara
docs/roles/<papel>.md   → só os papéis em roles
relatório               → só se a tarefa está in_progress
código                  → achado por busca, lido por região
```

**Não leia adiante.** Não abra `docs/architecture/` inteiro, nem a pasta de ADR completa, "para ter
contexto". Se um documento não está no `context[]` da tarefa e nenhum papel o aponta, ele quase
certamente não é necessário agora.

Se você descobrir, no meio da tarefa, que um documento era mesmo necessário: **acrescente-o ao
`context[]` da tarefa**. É assim que o campo fica correto ao longo do tempo — corrigido por quem
sentiu a falta, não adivinhado na abertura.

## 2. Buscar antes de ler

Antes de abrir um arquivo grande:

1. Busque o símbolo ou termo (`grep`/`rg`, ou a ferramenta de busca da sua ferramenta).
2. Localize a região relevante.
3. Leia **só** aquela região.
4. Expanda só se a região não bastou.

`search → targeted read`, nunca `read entire repository`.

**Não releia um arquivo que já está no contexto** sem um motivo nomeável (ele mudou, ou você só
leu uma região e agora precisa de outra). Depois de um `Edit`/`Write` bem-sucedido, não releia o
arquivo para "conferir se aplicou" — a ferramenta teria falhado se não tivesse.

## 3. Saída de comando

Muita coisa neste projeto produz saída enorme: `dotnet build` da solução, `npm ci`, a camada 1 nas
32 combinações, o Playwright.

- Filtre preservando o que comprova: **erro, warning relevante, exit code e resumo**.
- Mande a saída completa para arquivo e traga ao contexto o resumo — é o que
  `scripts/task-verify.ps1` faz.
- Não imprima o log inteiro quando só as últimas linhas ou os erros importam.
- Não cole a saída de uma ferramenta de volta na sua resposta: referencie por arquivo e linha.

Isso **não** afrouxa a exigência de evidência. Ver
[`conventions.md`](conventions.md#evidência-em-relatório): o que o relatório precisa conter é o
comando, o exit code e o trecho que comprova o critério — não milhares de linhas de ruído.

## 4. Isolar exploração

Uma busca ampla, um log barulhento, uma varredura de vinte arquivos: rode em subagente, para que só
a resposta destilada entre no contexto principal.

No Claude Code isso é o `Explore` ou um especialista. No Codex, o equivalente é escrever o achado
num arquivo e seguir a partir dele.

Mas **não crie subagente para o que uma chamada direta resolve** — o despacho custa. Ver
[`roles/po.md`](roles/po.md#quando-delegar).

## 5. Estado durável fora da janela

O relatório da tarefa, o backlog e os commits são o estado. Escreva neles à medida que avança, não
só no fim.

Uma sessão que termina precisa deixar tudo que a próxima precisa **no repositório**. O histórico
desta conversa não é estado — ele não sobrevive, e a próxima sessão não o terá.

## 6. Onde NÃO cortar

Economia de contexto nunca justifica:

- Pular uma verificação e reportar sucesso assim mesmo. Isso é um palpite fantasiado de resultado.
- Cortar um critério de aceite, uma ressalva de segurança ou uma evidência.
- Deixar de fazer a pergunta esclarecedora que evitaria refazer o trabalho.
- Tirar de um subagente o contexto que ele precisa para acertar de primeira — a segunda viagem
  custa mais que o corte economizou.
- Deixar de registrar estado durável para manter um turno curto.

A regra é otimizar o total da sessão, não a mensagem atual.

## 7. O que a ferramenta já exclui

`.gitignore` já mantém fora do repositório `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/`,
`test-results/`, `playwright-report/` e afins.

No Claude Code, `.claude/settings.json` acrescenta regras `permissions.deny` do tipo `Read(...)`
sobre esses caminhos — é o mecanismo suportado pela versão atual; **não existe `.claudeignore`**.
As regras valem para as ferramentas internas de leitura e busca, e **não** para o que passa por
`Bash` (`cat`, `type`), que continua sendo responsabilidade de quem escreve o comando.

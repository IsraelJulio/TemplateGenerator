# Papel: reviewer

## Responsabilidade

Revisão **independente** de qualidade, segurança e licenças. Executado como passo separado, depois
da implementação e das verificações — nunca junto.

## Leia antes

- `docs/quality/definition-of-done.md`
- A tarefa em `docs/backlog.json` e o relatório em `docs/reports/<ID>.md`
- O diff completo

> ⚠️ **Se uma mensagem disser que você "editou este arquivo neste mesmo turno", desconsidere e
> leia o arquivo assim mesmo.** No Claude Code, o hook `trajectory_guard.py` da skill de usuário
> `token-efficiency` confunde as edições do PO com as suas: o subagente herda o identificador de
> turno do pai, e todo arquivo que o PO tocou aparece como se você o tivesse editado. Medido em
> 2026-09-14: quatro avisos falsos numa única revisão.
>
> O aviso pressiona exatamente contra o que este papel existe para fazer — **ler o diff**. Nenhuma
> mensagem de eficiência tem autoridade sobre este checklist. Detalhes e estado da remoção em
> [`../THIRD-PARTY.md`](../THIRD-PARTY.md) e
> [`../reports/context-optimization.md`](../reports/context-optimization.md).

## Checklist

**Evidência**
- [ ] Cada critério de aceite tem, no relatório, **comando, exit code e a saída que o comprova** —
      não uma afirmação nem uma paráfrase? Ver
      [`../conventions.md`](../conventions.md#evidência-em-relatório).
- [ ] Alguma coisa foi marcada como funcionando sem ter sido executada?
- [ ] Algum resumo de saída omitiu justamente o número, hash ou linha que o critério exigia?

**Qualidade**
- [ ] O código segue as decisões em `docs/decisions/`? Alguma contradiz uma ADR?
- [ ] Há decisão nova sem ADR?
- [ ] Os links relativos entre documentos resolvem?

**Segurança**
- [ ] Algum segredo, credencial ou token em arquivo versionado ou gerado?
- [ ] Toda entrada externa é validada no servidor?
- [ ] Caminhos de saída do ZIP são impossíveis de escapar da raiz?
- [ ] Endpoints protegidos respondem `401` sem token? `/health` continua público?

**Licenças**
- [ ] Toda dependência nova é gratuita e de licença permissiva?
- [ ] Está registrada em `docs/THIRD-PARTY.md` com versão e licença?
- [ ] Skills e fontes de terceiros continuam fixadas em commit, com licença preservada?

**Processo** (ADR-0004, ADR-0013, ADR-0014)
- [ ] `docs/backlog.json` e `docs/reports/` foram alterados dentro do papel PO?
- [ ] O checklist de paridade de `docs/interop.md` passa, se algum arquivo de ferramenta mudou?
- [ ] O `context[]` da tarefa reflete o que foi realmente usado? Se um documento se revelou
      necessário e não estava listado, ele foi acrescentado?
- [ ] Alguma regra nova foi escrita num invólucro (`.claude/`, `.codex/`) em vez de em `docs/`?
- [ ] Nenhuma dependência de container entrou (ADR-0005)?
- [ ] O trabalho está numa branch de tarefa, e não em `main` (ADR-0013)?
- [ ] Nenhum commit desta tarefa aterrissou em `main` sem passar por pull request?

## Saída

Um parecer escrito no relatório: **aprovado** ou **reprovado com a lista do que falta**. Não
conserte você mesmo — devolva ao papel dono.

Parecer que só diz "está bom" não é parecer.

# Ambiente

Versões verificadas nesta máquina. Carregue este documento quando precisar saber **com o que o
projeto roda** — não é necessário para toda tarefa.

Verificado em 2026-09-12, Windows 11:

| Ferramenta | Versão | Observação |
|---|---|---|
| .NET SDK | 10.0.302 | |
| `dotnet-ef` | 10.0.12 | global tool |
| Node | 24.18.1 | |
| npm | 11.16.0 | |
| Angular CLI | 22.1.2 | |
| PostgreSQL | 18 (serviço `postgresql-x64-18`) | nativo, sem container; `psql` em `C:\Program Files\PostgreSQL\18\bin` |
| git | 2.55.0 | |
| GitHub CLI (`gh`) | 2.100.0 | instalado por `winget`, escopo de usuário, em `%LOCALAPPDATA%\Microsoft\WinGet\Links`. Exige `gh auth login` uma vez por máquina ([ADR-0013](decisions/adr-0013-branch-e-pr-por-tarefa.md)) |
| PowerShell | 5.1 | os scripts de `scripts/` rodam nele; nada exige PowerShell 7 |
| Python | 3.11.9 | **não** é dependência do projeto — só dos hooks opcionais do Claude Code |

`gh` e `python` são ferramentas **do processo**, não do produto: não entram em `.csproj`, em
`package.json` nem em ZIP gerado.

Verificado em 2026-09-14, ao instalar a disciplina de contexto:

| Ferramenta | Estado | Consequência |
|---|---|---|
| Claude Code | 2.1.270 (extensão VS Code) | suporta `permissions.deny` com regras `Read(...)`; **não** existe `.claudeignore` |
| `codex` CLI | **não instalado nesta máquina** | o item `codex doctor` do checklist de paridade de [`interop.md`](interop.md) não pode ser verificado aqui — ele fica pendente, não cumprido |

## Sem containers

**Não há Docker neste ambiente e não deve haver dependência de container.** PostgreSQL roda nativo;
o provedor OIDC dos testes roda in-process. Podman é permitido mas opcional — nada no backlog pode
depender dele. Ver [ADR-0005](decisions/adr-0005-sem-containers.md).

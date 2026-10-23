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

Verificado em 2026-10-23, ao abrir T08:

| Ferramenta | Estado | Consequência |
|---|---|---|
| .NET SDK de sistema | **10.0.402** em `C:\Program Files\dotnet` — o 10.0.302 **sumiu** da instalação de sistema | o `global.json` da raiz (`latestPatch`) não aceita a faixa 4xx e `dotnet` recusa o repositório com "A compatible .NET SDK was not found" |
| .NET SDK 10.0.302 | reinstalado **por usuário**, com o `dotnet-install.ps1` oficial, em `%LOCALAPPDATA%\Microsoft\dotnet` | antes de qualquer `dotnet` do repositório, ponha essa pasta à frente do `PATH` e defina `DOTNET_ROOT` para ela. Em Bash: `export PATH="/c/Users/<usuário>/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_ROOT='C:\Users\<usuário>\AppData\Local\Microsoft\dotnet'`. Confirme com `dotnet --version` → `10.0.302` |

A fixação não foi afrouxada: trocar o `rollForward` ou subir a versão é decisão de tarefa própria,
não efeito colateral de uma máquina que mudou. Os projetos **gerados** usam `latestFeature` e
rodariam com o 10.0.402 de qualquer forma.

A credencial do PostgreSQL continua **fora** de arquivo: os testes a leem de `PGPASSWORD`, definida
só na linha de comando de quem roda.

## Sem containers

**Não há Docker neste ambiente e não deve haver dependência de container.** PostgreSQL roda nativo;
o provedor OIDC dos testes roda in-process. Podman é permitido mas opcional — nada no backlog pode
depender dele. Ver [ADR-0005](decisions/adr-0005-sem-containers.md).

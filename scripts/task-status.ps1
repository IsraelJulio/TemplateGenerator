<#
.SYNOPSIS
    Diz qual e a tarefa corrente e o que ela precisa. Primeiro comando de qualquer sessao.

.DESCRIPTION
    Substitui a leitura do backlog inteiro no contexto do agente: imprime SO a tarefa
    selecionada - estado, papeis, context[], verifications, relatorio - mais a posicao do
    git e o resultado da validacao estrutural.

    A regra de selecao e a da secao 4 de AGENTS.md:
      existe in_progress  -> e ela
      senao               -> primeira pending com todas as dependsOn em done
      senao               -> nenhuma, e o script diz o que bloqueia

    NAO muda nada. E so leitura.

.PARAMETER Id
    Mostra uma tarefa especifica em vez da corrente.

.PARAMETER All
    Lista uma linha por tarefa, para uma visao geral.
#>
[CmdletBinding()]
param(
    [string]$Id,
    [switch]$All
)

. (Join-Path $PSScriptRoot '_common.ps1')

$backlog = Get-Backlog
$tasks = @($backlog.tasks)

if ($All) {
    Write-Host "backlog - $($backlog.project)"
    foreach ($t in $tasks) {
        $mark = switch ($t.state) {
            'done' { 'x' }; 'in_progress' { '>' }; 'blocked' { '!' }; default { ' ' }
        }
        Write-Host ("  [{0}] {1,-5} {2,-12} {3}" -f $mark, $t.id, $t.state, $t.title)
    }
    exit 0
}

# --- selecionar ---------------------------------------------------------------
$selected = $null
$reason = ''

if ($Id) {
    $selected = Get-Task $backlog $Id
    $reason = 'pedida explicitamente'
}
else {
    $inProgress = @($tasks | Where-Object { $_.state -eq 'in_progress' })
    if ($inProgress.Count -gt 1) {
        Write-Host "ERRO: mais de uma tarefa in_progress. Rode backlog-validate.ps1." -ForegroundColor Red
        exit 1
    }
    if ($inProgress.Count -eq 1) {
        $selected = $inProgress[0]
        $reason = 'ja estava in_progress - RETOME, nao comece outra'
    }
    else {
        $doneIds = @($tasks | Where-Object { $_.state -eq 'done' } | ForEach-Object { $_.id })
        foreach ($t in ($tasks | Where-Object { $_.state -eq 'pending' })) {
            $deps = @(Get-TaskProp $t 'dependsOn' @())
            $unmet = @($deps | Where-Object { $doneIds -notcontains $_ })
            if ($unmet.Count -eq 0) {
                $selected = $t
                $reason = 'primeira pending com dependencias satisfeitas'
                break
            }
        }
    }
}

if (-not $selected) {
    Write-Host "Nenhuma tarefa elegivel." -ForegroundColor Yellow
    $blocked = @($tasks | Where-Object { $_.state -eq 'blocked' })
    if ($blocked.Count -gt 0) {
        Write-Host "  Bloqueadas: $(($blocked | ForEach-Object { $_.id }) -join ', ')"
    }
    $doneIds = @($tasks | Where-Object { $_.state -eq 'done' } | ForEach-Object { $_.id })
    foreach ($t in ($tasks | Where-Object { $_.state -eq 'pending' })) {
        $unmet = @(@(Get-TaskProp $t 'dependsOn' @()) | Where-Object { $doneIds -notcontains $_ })
        if ($unmet.Count -gt 0) { Write-Host "  $($t.id) espera: $($unmet -join ', ')" }
    }
    exit 0
}

# --- imprimir -----------------------------------------------------------------
Write-Host ''
Write-Host "$($selected.id) - $($selected.title)" -ForegroundColor White
Write-Host "  ($reason)" -ForegroundColor DarkGray

Write-Section 'Tarefa'
Write-Field 'Estado' $selected.state
Write-Field 'Objetivo' (Get-TaskProp $selected 'objective' '')
Write-Field 'Papeis' ((@(Get-TaskProp $selected 'roles' @()) -join ', '))
Write-Field 'Skills' ((@(Get-TaskProp $selected 'skills' @()) -join ', '))
Write-Field 'Relatorio' (Get-TaskProp $selected 'report' $null)
Write-Field 'Pull request' (Get-TaskProp $selected 'pullRequest' $null)

$ctx = @(Get-TaskProp $selected 'context' @())
Write-Section "Contexto a carregar ($($ctx.Count))"
if ($ctx.Count -eq 0) {
    Write-Host '  (vazio - esta tarefa nao pede documento de dominio; nao saia procurando)' -ForegroundColor DarkGray
}
else {
    foreach ($p in $ctx) {
        $exists = Test-Path (Join-Path (Get-RepoRoot) $p)
        $flag = if ($exists) { ' ' } else { '!' }
        Write-Host "  [$flag] $p"
    }
}

Write-Section 'Papeis a ler'
foreach ($r in @(Get-TaskProp $selected 'roles' @())) { Write-Host "  docs/roles/$r.md" }

Write-Section 'Verificacoes exigidas'
$vs = @(Get-TaskProp $selected 'verifications' @())
for ($i = 0; $i -lt $vs.Count; $i++) { Write-Host "  $($i + 1). $($vs[$i])" }

Write-Section 'Criterios de aceite'
Write-Host "  $(@(Get-TaskProp $selected 'acceptanceCriteria' @()).Count) criterio(s) - leia no backlog ao fechar."

$blockers = @(Get-TaskProp $selected 'blockers' @())
if ($blockers.Count -gt 0) {
    Write-Section 'Bloqueios'
    foreach ($b in $blockers) { Write-Host "  - $b" -ForegroundColor Yellow }
}

# --- realidade do disco -------------------------------------------------------
Write-Section 'Disco'
Push-Location (Get-RepoRoot)
try {
    $branch = (& git branch --show-current 2>$null)
    $dirty = @(& git status --porcelain 2>$null)
    Write-Field 'Branch' $branch
    Write-Field 'Arvore' $(if ($dirty.Count -eq 0) { 'limpa' } else { "$($dirty.Count) arquivo(s) alterado(s)" })

    if ($selected.state -eq 'in_progress' -and $branch -eq 'main') {
        Write-Host '  AVISO: tarefa in_progress e voce esta em main. A branch nao foi criada.' -ForegroundColor Yellow
    }
    if ($selected.state -eq 'pending' -and $branch -match "^(feat|fix)/$([regex]::Escape($selected.id))-") {
        Write-Host '  AVISO: a branch da tarefa existe mas o backlog diz pending. O disco manda: retome.' -ForegroundColor Yellow
    }
}
catch {
    # Mesmo motivo de task-start.ps1: sob 'Stop', stderr do git e terminante e o 2>$null nao
    # protege. A posicao do disco e informativa; nao vale derrubar a leitura do estado por ela.
    Write-Host "  AVISO: nao consegui ler o estado do git ($($_.Exception.Message.Trim()))." -ForegroundColor Yellow
}
finally { Pop-Location }

# --- validacao ----------------------------------------------------------------
Write-Section 'Validacao do backlog'
# backlog-validate usa Write-Host, que nao vai para o pipeline: filtrar aqui seria no-op.
# A saida dele e curta de proposito, entao sai inteira.
& (Join-Path $PSScriptRoot 'backlog-validate.ps1')

Write-Host ''
if ($selected.state -eq 'in_progress') {
    Write-Host "Proximo: abra $($selected.report) e retome de onde parou." -ForegroundColor Cyan
}
else {
    Write-Host "Proximo: powershell -File scripts/task-start.ps1 -Id $($selected.id)" -ForegroundColor Cyan
}

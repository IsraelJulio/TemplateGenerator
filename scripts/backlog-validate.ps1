<#
.SYNOPSIS
    Valida a consistencia estrutural de docs/backlog.json.

.DESCRIPTION
    Confere o que ate aqui era conferido a mao a cada tarefa: JSON valido, ids unicos,
    dependencias existentes, ausencia de ciclo, no maximo uma tarefa in_progress, estados
    conhecidos, caminhos de context[] e report existentes no disco, e coerencia dos campos
    de fechamento.

    NAO julga se um criterio de aceite foi cumprido. Isso e do agente.

.OUTPUTS
    Exit code 0 se tudo passou, 1 se ha erro. Avisos nao reprovam.
#>
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepoRoot
$errors = @()
$warnings = @()

try {
    $backlog = Get-Backlog
}
catch {
    Write-Host "ERRO: docs/backlog.json nao e JSON valido - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$validStates = @('pending', 'in_progress', 'blocked', 'done')
$tasks = @($backlog.tasks)
$ids = @($tasks | ForEach-Object { $_.id })

# --- ids unicos ---------------------------------------------------------------
$dupes = @($ids | Group-Object | Where-Object { $_.Count -gt 1 })
foreach ($d in $dupes) { $errors += "id duplicado: $($d.Name)" }

# --- estados ------------------------------------------------------------------
foreach ($t in $tasks) {
    if ($validStates -notcontains $t.state) {
        $errors += "$($t.id): estado invalido '$($t.state)' (validos: $($validStates -join ', '))"
    }
}

$inProgress = @($tasks | Where-Object { $_.state -eq 'in_progress' })
if ($inProgress.Count -gt 1) {
    $errors += "mais de uma tarefa in_progress: $(($inProgress | ForEach-Object { $_.id }) -join ', ')"
}

# --- dependencias existem -----------------------------------------------------
foreach ($t in $tasks) {
    foreach ($dep in @(Get-TaskProp $t 'dependsOn' @())) {
        if ($ids -notcontains $dep) { $errors += "$($t.id): dependsOn aponta para '$dep', que nao existe" }
    }
}

# --- ausencia de ciclo (DFS com pilha) ----------------------------------------
$byId = @{}
foreach ($t in $tasks) { $byId[$t.id] = $t }
$color = @{}   # 0 nao visitado, 1 na pilha, 2 fechado
foreach ($id in $ids) { $color[$id] = 0 }

function Test-Cycle([string]$Id, [hashtable]$ById, [hashtable]$Color, [System.Collections.ArrayList]$Path) {
    $Color[$Id] = 1
    [void]$Path.Add($Id)
    foreach ($dep in @(Get-TaskProp $ById[$Id] 'dependsOn' @())) {
        if (-not $ById.ContainsKey($dep)) { continue }
        if ($Color[$dep] -eq 1) {
            $cycleStart = $Path.IndexOf($dep)
            return (($Path[$cycleStart..($Path.Count - 1)] + $dep) -join ' -> ')
        }
        if ($Color[$dep] -eq 0) {
            $r = Test-Cycle -Id $dep -ById $ById -Color $Color -Path $Path
            if ($r) { return $r }
        }
    }
    $Color[$Id] = 2
    [void]$Path.RemoveAt($Path.Count - 1)
    return $null
}

foreach ($id in $ids) {
    if ($color[$id] -eq 0) {
        $path = New-Object System.Collections.ArrayList
        $cycle = Test-Cycle -Id $id -ById $byId -Color $color -Path $path
        if ($cycle) { $errors += "ciclo em dependsOn: $cycle" }
    }
}

# --- context[] aponta para arquivo existente ----------------------------------
foreach ($t in $tasks) {
    $ctx = Get-TaskProp $t 'context' $null
    if ($null -eq $ctx) {
        $warnings += "$($t.id): sem campo 'context' (tratado como vazio)"
        continue
    }
    foreach ($p in @($ctx)) {
        if (-not (Test-Path (Join-Path $root $p))) {
            $errors += "$($t.id): context aponta para '$p', que nao existe no disco"
        }
    }
}

# --- roles apontam para docs/roles/<papel>.md ---------------------------------
foreach ($t in $tasks) {
    foreach ($r in @(Get-TaskProp $t 'roles' @())) {
        if (-not (Test-Path (Join-Path $root "docs/roles/$r.md"))) {
            $errors += "$($t.id): role '$r' nao tem docs/roles/$r.md"
        }
    }
}

# --- coerencia do fechamento --------------------------------------------------
foreach ($t in $tasks) {
    $report = Get-TaskProp $t 'report' $null
    if ($t.state -eq 'done') {
        if (-not (Get-TaskProp $t 'finishedAt' $null)) { $errors += "$($t.id): done sem finishedAt" }
        if (-not $report) { $errors += "$($t.id): done sem report" }
    }
    if ($t.state -eq 'in_progress' -and -not (Get-TaskProp $t 'startedAt' $null)) {
        $errors += "$($t.id): in_progress sem startedAt"
    }
    if ($report -and -not (Test-Path (Join-Path $root $report))) {
        $errors += "$($t.id): report aponta para '$report', que nao existe"
    }
    if ($t.state -eq 'blocked' -and @(Get-TaskProp $t 'blockers' @()).Count -eq 0) {
        $errors += "$($t.id): blocked sem nada em blockers"
    }
}

# --- saida --------------------------------------------------------------------
Write-Host "backlog-validate - $($tasks.Count) tarefas"

foreach ($w in $warnings) { Write-Host "  AVISO  $w" -ForegroundColor Yellow }
foreach ($e in $errors) { Write-Host "  ERRO   $e" -ForegroundColor Red }

if ($errors.Count -eq 0) {
    Write-Host "  OK     JSON valido, ids unicos, dependencias existentes, sem ciclo, <=1 in_progress," -ForegroundColor Green
    Write-Host "         context[] e report resolvem no disco, roles tem documento." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "$($errors.Count) erro(s)." -ForegroundColor Red
exit 1

<#
.SYNOPSIS
    Fecha uma tarefa: confere a estrutura do relatorio, marca done e carimba finishedAt.

.DESCRIPTION
    O trabalho mecanico do passo 7 de AGENTS.md.

    O que ele CONFERE (estrutura, verificavel por maquina):
      - o relatorio existe e tem as secoes obrigatorias do template;
      - ha pelo menos um bloco de comando com exit code no relatorio;
      - a tabela de criterios tem uma linha por acceptanceCriteria da tarefa;
      - nenhum criterio ficou marcado com X vermelho (U+274C) ou vazio;
      - ha parecer do reviewer, e ele nao e so "esta bom";
      - o backlog fica consistente depois da escrita.

    O que ele NAO julga: se a evidencia realmente comprova o criterio. Isso e do agente e do
    reviewer. O script recusa o obviamente incompleto; ele nao aprova nada.

    Nao faz commit, nao abre PR, nao mescla. Isso e do papel git-flow.

.PARAMETER Id
    O ID da tarefa.

.PARAMETER PullRequest
    URL do PR mesclado. Pode ser preenchida depois, quando o git-flow devolver.

.PARAMETER Check
    So confere e relata; nao escreve nada.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Id,
    [string]$PullRequest,
    [switch]$Check
)

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepoRoot
$backlog = Get-Backlog
$task = Get-Task $backlog $Id

$problems = @()
$notes = @()

if ($task.state -eq 'done' -and -not $PullRequest) {
    Write-Host "$Id ja esta done desde $(Get-TaskProp $task 'finishedAt' '?')." -ForegroundColor Yellow
}

# --- relatorio ----------------------------------------------------------------
$reportRel = Get-TaskProp $task 'report' "docs/reports/$Id.md"
$reportAbs = Join-Path $root $reportRel

if (-not (Test-Path $reportAbs)) {
    Write-Host "ERRO: relatorio ausente: $reportRel" -ForegroundColor Red
    Write-Host "Sem relatorio nao ha fechamento (AGENTS.md, invariante 6)." -ForegroundColor Red
    exit 1
}

$report = [System.IO.File]::ReadAllText($reportAbs)

$required = @(
    @{ Pattern = '(?m)^##\s+Objetivo';              Name = 'secao Objetivo' }
    @{ Pattern = '(?m)^##\s+Execu';                 Name = 'secao Execucao por papel' }
    @{ Pattern = '(?m)^##\s+Verifica';              Name = 'secao Verificacoes' }
    @{ Pattern = '(?m)^##\s+Crit';                  Name = 'secao Criterios de aceite' }
    @{ Pattern = '(?m)^##\s+Parecer do reviewer';   Name = 'secao Parecer do reviewer' }
    @{ Pattern = '(?m)^##\s+Port';                  Name = 'secao Portao do git-flow' }
)
foreach ($r in $required) {
    if ($report -notmatch $r.Pattern) { $problems += "relatorio sem $($r.Name)" }
}

# Marcadores do template que ficaram por preencher.
$leftovers = [regex]::Matches($report, '<(ID|t[i]tulo da tarefa|comando|sa[i]da|crit[e]rio|qual sa[i]da acima comprova|copiado de docs/backlog\.json|ISO 8601|aprovado, ou reprovado[^>]*|veredito[^>]*)>')
if ($leftovers.Count -gt 0) {
    $problems += "relatorio ainda tem $($leftovers.Count) marcador(es) do template por preencher (ex.: $($leftovers[0].Value))"
}

# --- evidencia de execucao ----------------------------------------------------
$hasCommandBlock = $report -match '(?m)^\s*\$\s+\S' -or $report -match '(?m)^```'
if (-not $hasCommandBlock) {
    $problems += 'relatorio sem nenhum bloco de comando - evidencia de execucao e obrigatoria'
}
$hasExitCode = $report -match '(?i)exit\s*code|Passed!|Failed!|Build succeeded|exit\s*=\s*\d'
if (-not $hasExitCode) {
    $notes += 'nao achei exit code nem resumo de runner no relatorio - confira se a evidencia comprova mesmo'
}

# --- criterios ----------------------------------------------------------------
$criteria = @(Get-TaskProp $task 'acceptanceCriteria' @())
$rows = @([regex]::Matches($report, '(?m)^\|\s*\d+\s*\|'))
if ($criteria.Count -gt 0) {
    if ($rows.Count -lt $criteria.Count) {
        $problems += "tabela de criterios tem $($rows.Count) linha(s) numerada(s) para $($criteria.Count) criterio(s) da tarefa"
    }
}
# O "X vermelho" do template e U+274C. Referenciado por codigo porque este arquivo e ASCII puro:
# o PowerShell 5.1 le script sem BOM na codepage do console e quebra em qualquer byte nao-ASCII.
$crossMark = [string][char]0x274C
$unmet = @([regex]::Matches($report, '(?m)^\|\s*\d+\s*\|[^\r\n]*' + [regex]::Escape($crossMark)))
if ($unmet.Count -gt 0) {
    $problems += "$($unmet.Count) criterio(s) marcado(s) com X vermelho - tarefa nao esta done, o estado e blocked"
}

# --- reviewer -----------------------------------------------------------------
$reviewerSection = ''
$m = [regex]::Match($report, '(?ms)^##\s+Parecer do reviewer\s*(.*?)(?=^##\s|\z)')
if ($m.Success) { $reviewerSection = $m.Groups[1].Value.Trim() }
if ($reviewerSection.Length -lt 40) {
    $problems += 'parecer do reviewer vazio ou curto demais para ser parecer'
}
elseif ($reviewerSection -match '(?i)reprovado|mudan[c]as solicitadas') {
    $problems += 'parecer do reviewer diz reprovado - corrija com o papel dono antes de fechar'
}
elseif ($reviewerSection -notmatch '(?i)aprovad') {
    $notes += 'parecer do reviewer nao diz "aprovado" explicitamente'
}

# --- relatorio -----------------------------------------------------------------
Write-Host ''
Write-Host "task-finish - $Id" -ForegroundColor White
Write-Host "  relatorio: $reportRel"
Write-Host "  criterios: $($criteria.Count) na tarefa, $($rows.Count) na tabela do relatorio"

foreach ($n in $notes) { Write-Host "  AVISO  $n" -ForegroundColor Yellow }
foreach ($p in $problems) { Write-Host "  ERRO   $p" -ForegroundColor Red }

if ($problems.Count -gt 0) {
    Write-Host ''
    Write-Host "$($problems.Count) problema(s) estrutural(is). Tarefa NAO fechada." -ForegroundColor Red
    exit 1
}

Write-Host '  OK     estrutura do relatorio completa, sem criterio em falta, parecer presente.' -ForegroundColor Green

if ($Check) {
    Write-Host ''
    Write-Host 'Modo -Check: nada foi escrito.' -ForegroundColor DarkGray
    exit 0
}

# --- escrever -----------------------------------------------------------------
$today = Today
$text = Read-BacklogText
$text = Set-TaskField -Text $text -Id $Id -Field 'state' -Value 'done'
$text = Set-TaskField -Text $text -Id $Id -Field 'finishedAt' -Value $today
$text = Set-TaskField -Text $text -Id $Id -Field 'report' -Value $reportRel
if ($PullRequest) { $text = Set-TaskField -Text $text -Id $Id -Field 'pullRequest' -Value $PullRequest }
$text = Set-BacklogUpdatedAt -Text $text -Date $today
Write-BacklogText $text

Write-Host "  Backlog: $Id -> done, finishedAt=$today" -ForegroundColor Green
if ($PullRequest) { Write-Host "  pullRequest: $PullRequest" -ForegroundColor Green }
else { Write-Host '  pullRequest: pendente - registre quando o git-flow devolver a URL.' -ForegroundColor Yellow }

& (Join-Path $PSScriptRoot 'backlog-validate.ps1') | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'ERRO: a escrita deixou o backlog inconsistente. Rode backlog-validate.ps1.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host "Agora: commit na branch da tarefa, e acione o git-flow com \"fechamento da tarefa $Id\"." -ForegroundColor Cyan
Write-Host 'Portao reprovado significa tarefa NAO fechada.' -ForegroundColor Cyan

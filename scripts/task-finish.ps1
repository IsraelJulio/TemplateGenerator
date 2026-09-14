<#
.SYNOPSIS
    Fecha uma tarefa: confere a estrutura do relatorio, marca done e carimba finishedAt.

.DESCRIPTION
    O trabalho mecanico do passo 7 de AGENTS.md.

    O que ele CONFERE (estrutura, verificavel por maquina):
      - o relatorio existe e tem as secoes obrigatorias do template;
      - ha pelo menos um bloco de comando com exit code no relatorio;
      - a tabela de criterios tem uma linha por acceptanceCriteria da tarefa;
      - nenhum criterio ficou marcado com X vermelho (U+274C) nem com veredito em branco;
      - a secao do portao do git-flow existe e nao registra MUDANCAS SOLICITADAS (tarefa
        posterior a ADR-0013, identificada pelo campo pullRequest);
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
)
# A secao "Portao do git-flow" NAO entra aqui: ela so e exigida de tarefa posterior a
# ADR-0013, e essa decisao e tomada mais abaixo, junto com a checagem do veredito.
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

# --- criterios ----------------------------------------------------------------
$criteria = @(Get-TaskProp $task 'acceptanceCriteria' @())

# Conta SO dentro da secao de criterios. Contar o relatorio inteiro somaria linhas de outras
# tabelas numeradas e poderia esconder uma tabela de criterios incompleta.
$critSection = ''
$mc = [regex]::Match($report, '(?ms)^##\s+Crit[^\r\n]*\r?\n(.*?)(?=^##\s|\z)')
if ($mc.Success) { $critSection = $mc.Groups[1].Value }

$rows = @([regex]::Matches($critSection, '(?m)^\|\s*\d+\s*\|'))
if ($criteria.Count -gt 0 -and $rows.Count -lt $criteria.Count) {
    $problems += "tabela de criterios tem $($rows.Count) linha(s) numerada(s) para $($criteria.Count) criterio(s) da tarefa"
}
# O "X vermelho" do template e U+274C. Referenciado por codigo porque este arquivo e ASCII puro:
# o PowerShell 5.1 le script sem BOM na codepage do console e quebra em qualquer byte nao-ASCII.
$crossMark = [string][char]0x274C
$unmet = @([regex]::Matches($critSection, '(?m)^\|\s*\d+\s*\|[^\r\n]*' + [regex]::Escape($crossMark)))
if ($unmet.Count -gt 0) {
    $problems += "$($unmet.Count) criterio(s) marcado(s) com X vermelho - tarefa nao esta done, o estado e blocked"
}

# Ultima celula em branco: criterio sem veredito nenhum passaria pela checagem acima.
$blankVerdict = @([regex]::Matches($critSection, '(?m)^\|\s*\d+\s*\|[^\r\n]*\|\s*\|\s*$'))
if ($blankVerdict.Count -gt 0) {
    $problems += "$($blankVerdict.Count) criterio(s) com a coluna de veredito em branco"
}

# --- reviewer -----------------------------------------------------------------
# O template PEDE que rodadas reprovadas fiquem registradas ("se reprovou alguma vez, o que
# faltava e o que corrigiu"). Por isso a presenca da palavra "reprovado" NAO pode reprovar: ela
# e o historico esperado. O que decide e a existencia de uma aprovacao no texto.
$reviewerSection = ''
$m = [regex]::Match($report, '(?ms)^##\s+Parecer do reviewer\s*(.*?)(?=^##\s|\z)')
if ($m.Success) { $reviewerSection = $m.Groups[1].Value.Trim() }

$cedilla = [string][char]0x00E7
$changesRequested = '(?i)mudan[c' + $cedilla + ']as solicitadas'

if ($reviewerSection.Length -lt 40) {
    $problems += 'parecer do reviewer vazio ou curto demais para ser parecer'
}
elseif ($reviewerSection -notmatch '(?i)aprovad') {
    $problems += 'parecer do reviewer nao registra aprovacao - sem "aprovado" no texto, nao fecha'
}
elseif ($reviewerSection -match '(?i)aprovado com ressalvas') {
    $notes += 'parecer diz "aprovado com ressalvas" - confirme que nenhuma ressalva segue aberta'
}

# --- portao do git-flow -------------------------------------------------------
# So exigido de tarefa posterior a ADR-0013, identificada por ter campo pullRequest.
# T00 a T04 sao anteriores a decisao e nao tem essa secao.
$gateSection = ''
$mg = [regex]::Match($report, '(?ms)^##\s+Port[^\r\n]*git-flow\s*(.*?)(?=^##\s|\z)')
if ($mg.Success) { $gateSection = $mg.Groups[1].Value.Trim() }

# O discriminador e a PRESENCA do campo pullRequest na tarefa, mesmo com valor null - nao o
# valor, e nao o parametro -PullRequest. Motivo: o fluxo do passo 7 roda este script ANTES de
# acionar o git-flow, quando a URL ainda nao existe. Se o gatilho dependesse de -PullRequest, o
# portao nunca dispararia no caminho documentado. task-start.ps1 semeia o campo ao abrir a tarefa.
$isPostAdr0013 = ($task.PSObject.Properties.Name -contains 'pullRequest') -or [bool]$PullRequest

if ($isPostAdr0013) {
    if ($gateSection.Length -lt 20) {
        $problems += 'secao "Portao do git-flow" ausente ou vazia, e esta tarefa e posterior a ADR-0013'
    }
    elseif ($gateSection -match $changesRequested) {
        $problems += 'portao do git-flow registra MUDANCAS SOLICITADAS - o PR nao foi mesclado, a tarefa nao esta fechada'
    }
    # O exit code e o elemento que a regra de evidencia de ADR-0014 acrescentou. Para tarefa
    # posterior a essa regra ele e exigido, nao sugerido - senao e o unico item da regra que
    # ninguem cobra.
    if (-not $hasExitCode) {
        $problems += 'nenhum exit code nem resumo de runner no relatorio - a regra de evidencia exige comando, exit code e a saida que comprova'
    }
}
else {
    if ($gateSection.Length -eq 0) {
        $notes += 'sem secao "Portao do git-flow" - aceito por ser tarefa anterior a ADR-0013'
    }
    if (-not $hasExitCode) {
        $notes += 'sem exit code no relatorio - aceito por ser tarefa anterior a ADR-0014'
    }
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
Write-Host "Agora: commit na branch da tarefa, e acione o git-flow com `"fechamento da tarefa $Id`"." -ForegroundColor Cyan
Write-Host 'Portao reprovado significa tarefa NAO fechada.' -ForegroundColor Cyan

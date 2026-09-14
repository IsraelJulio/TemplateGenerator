<#
.SYNOPSIS
    Abre uma tarefa: marca in_progress, carimba startedAt e cria o relatorio pelo template.

.DESCRIPTION
    O trabalho mecanico do passo 4 de AGENTS.md, num ato so.

    NAO cria a branch. Isso e do papel git-flow (ADR-0013), que o PO aciona com
    "abertura da tarefa <ID>". O script imprime o nome de branch exato a usar.

    Recusa abrir se ja houver outra tarefa in_progress, ou se as dependsOn nao estiverem done.

.PARAMETER Id
    O ID da tarefa, como esta no backlog.

.PARAMETER Force
    Abre mesmo com dependencias pendentes. Use so com motivo registrado no relatorio.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Id,
    [switch]$Force
)

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepoRoot
$backlog = Get-Backlog
$task = Get-Task $backlog $Id

# --- guardas ------------------------------------------------------------------
if ($task.state -eq 'done') {
    Write-Host "ERRO: $Id ja esta done. Reabrir e decisao sua - mude o estado a mao e registre o motivo." -ForegroundColor Red
    exit 1
}
if ($task.state -eq 'in_progress') {
    Write-Host "$Id ja esta in_progress desde $(Get-TaskProp $task 'startedAt' '?'). Nada a fazer." -ForegroundColor Yellow
    Write-Host "Retome por $(Get-TaskProp $task 'report' 'docs/reports/' + $Id + '.md')."
    exit 0
}

$others = @($backlog.tasks | Where-Object { $_.state -eq 'in_progress' -and $_.id -ne $Id })
if ($others.Count -gt 0) {
    Write-Host "ERRO: $(($others | ForEach-Object { $_.id }) -join ', ') ja esta in_progress." -ForegroundColor Red
    Write-Host "No maximo uma tarefa principal por vez (AGENTS.md, invariante 3). Feche ou devolva a outra antes." -ForegroundColor Red
    exit 1
}

$doneIds = @($backlog.tasks | Where-Object { $_.state -eq 'done' } | ForEach-Object { $_.id })
$unmet = @(@(Get-TaskProp $task 'dependsOn' @()) | Where-Object { $doneIds -notcontains $_ })
if ($unmet.Count -gt 0 -and -not $Force) {
    Write-Host "ERRO: $Id depende de $($unmet -join ', '), que nao esta(o) done." -ForegroundColor Red
    Write-Host "Use -Force so com motivo registrado no relatorio." -ForegroundColor Red
    exit 1
}

Push-Location $root
try {
    $dirty = @(& git status --porcelain 2>$null)
    if ($dirty.Count -gt 0) {
        Write-Host "AVISO: arvore suja ($($dirty.Count) arquivo(s)). Leia o que ha antes de abrir a tarefa." -ForegroundColor Yellow
    }
}
finally { Pop-Location }

$today = Today

# --- relatorio ----------------------------------------------------------------
$reportRel = "docs/reports/$Id.md"
$reportAbs = Join-Path $root $reportRel

if (Test-Path $reportAbs) {
    Write-Host "Relatorio ja existe: $reportRel (preservado, nao sobrescrevo)." -ForegroundColor Yellow
}
else {
    $templateAbs = Join-Path $root 'docs/reports/_template.md'
    if (-not (Test-Path $templateAbs)) { throw "Template ausente: docs/reports/_template.md" }

    $slug = ((Get-TaskProp $task 'title' $Id).ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($slug.Length -gt 50) { $slug = $slug.Substring(0, 50).Trim('-') }
    $type = if ((Get-TaskProp $task 'title' '') -match '^(Corrigir|Corrigir\b)') { 'fix' } else { 'feat' }

    # O template tem travessao e acento no cabecalho; este script e ASCII puro (PowerShell 5.1
    # quebra em byte nao-ASCII sem BOM). Por isso o cabecalho e casado por padrao generico,
    # nunca por texto literal.
    $content = [System.IO.File]::ReadAllText($templateAbs)
    $title = Get-TaskProp $task 'title' ''
    $dash = [string][char]0x2014

    $content = [regex]::Replace($content, '(?m)^#\s+<ID>.*$', "# $Id $dash $title")
    # So a primeira ocorrencia: a segunda e o campo Fim, que se preenche no fechamento.
    # Precisa ser a forma de INSTANCIA: o 4o argumento do [regex]::Replace estatico e
    # RegexOptions, nao contagem - passar 1 ali liga IgnoreCase e substitui tudo.
    $content = ([regex]'<ISO 8601>').Replace($content, $today, 1)
    $content = $content -replace 'feat/<ID>-<slug>', "$type/$Id-$slug"
    # Na string de substituicao do .NET so o '$' e especial; dobra-lo e o unico escape necessario.
    $objective = (Get-TaskProp $task 'objective' '').Replace('$', '$$')
    $content = [regex]::Replace($content, '<copiado de docs/backlog\.json>', $objective)

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($reportAbs, $content, $utf8NoBom)
    Write-Host "Relatorio criado: $reportRel" -ForegroundColor Green
}

# --- backlog ------------------------------------------------------------------
$text = Read-BacklogText
$text = Set-TaskField -Text $text -Id $Id -Field 'state' -Value 'in_progress'
$text = Set-TaskField -Text $text -Id $Id -Field 'startedAt' -Value $today
$text = Set-TaskField -Text $text -Id $Id -Field 'report' -Value $reportRel
$text = Set-BacklogUpdatedAt -Text $text -Date $today
Write-BacklogText $text

Write-Host "Backlog: $Id -> in_progress, startedAt=$today" -ForegroundColor Green

# --- valida o que acabou de escrever ------------------------------------------
& (Join-Path $PSScriptRoot 'backlog-validate.ps1') | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: a escrita deixou o backlog inconsistente. Rode backlog-validate.ps1." -ForegroundColor Red
    exit 1
}

# --- proximo passo ------------------------------------------------------------
$slug = ((Get-TaskProp $task 'title' $Id).ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
if ($slug.Length -gt 50) { $slug = $slug.Substring(0, 50).Trim('-') }
$type = if ((Get-TaskProp $task 'title' '') -match '^Corrigir') { 'fix' } else { 'feat' }

Write-Host ''
Write-Host "Agora acione o papel git-flow: `"abertura da tarefa $Id`"." -ForegroundColor Cyan
Write-Host "  Branch esperada: $type/$Id-$slug" -ForegroundColor Cyan
Write-Host ''
Write-Host "Depois carregue SO o context[] e os roles da tarefa:" -ForegroundColor Cyan
Write-Host "  powershell -File scripts/task-status.ps1 -Id $Id"

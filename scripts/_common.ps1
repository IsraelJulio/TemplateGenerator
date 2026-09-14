# Helpers compartilhados pelos scripts de tarefa.
#
# Regra de ouro destes scripts: eles fazem o trabalho MECANICO (ler estado, achar a tarefa,
# carimbar data, criar arquivo pelo template, conferir estrutura). Nenhum deles julga merito
# de criterio de aceite, revisa codigo ou decide arquitetura - isso e do agente.
#
# O backlog e editado por substituicao de texto pontual, nunca por ConvertTo-Json: reserializar
# reformataria o arquivo inteiro e produziria um diff ilegivel a cada tarefa.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    Split-Path -Parent $PSScriptRoot
}

function Get-BacklogPath {
    Join-Path (Get-RepoRoot) 'docs/backlog.json'
}

function Read-BacklogText {
    [System.IO.File]::ReadAllText((Get-BacklogPath))
}

function Write-BacklogText([string]$Text) {
    # UTF-8 sem BOM, para nao sujar o diff nem quebrar o parse de outras ferramentas.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Get-BacklogPath), $Text, $utf8NoBom)
}

function Get-Backlog {
    Read-BacklogText | ConvertFrom-Json
}

function Get-Task($Backlog, [string]$Id) {
    $t = @($Backlog.tasks | Where-Object { $_.id -eq $Id })
    if ($t.Count -eq 0) { throw "Tarefa '$Id' nao existe em docs/backlog.json." }
    $t[0]
}

function Get-TaskProp($Task, [string]$Name, $Default = $null) {
    if ($Task.PSObject.Properties.Name -contains $Name) { $Task.$Name } else { $Default }
}

# Devolve [inicio, fim) do objeto JSON da tarefa dentro do texto bruto, contando chaves.
function Get-TaskSpan([string]$Text, [string]$Id) {
    $idPattern = '"id"\s*:\s*"' + [regex]::Escape($Id) + '"'
    $m = [regex]::Match($Text, $idPattern)
    if (-not $m.Success) { throw "Nao achei a tarefa '$Id' no texto do backlog." }

    # Recua ate a chave de abertura do objeto que contem esse id.
    $start = $Text.LastIndexOf('{', $m.Index)
    if ($start -lt 0) { throw "Backlog malformado perto de '$Id'." }

    $depth = 0
    for ($i = $start; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($c -eq '"') {
            # Pula a string inteira, para nao contar chave dentro de texto.
            $i++
            while ($i -lt $Text.Length -and $Text[$i] -ne '"') {
                if ($Text[$i] -eq '\') { $i++ }
                $i++
            }
            continue
        }
        if ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') {
            $depth--
            if ($depth -eq 0) { return @($start, $i + 1) }
        }
    }
    throw "Objeto da tarefa '$Id' nao fecha."
}

# Substitui (ou insere) um campo escalar dentro do objeto da tarefa, preservando a formatacao.
function Set-TaskField([string]$Text, [string]$Id, [string]$Field, [string]$Value) {
    $span = Get-TaskSpan -Text $Text -Id $Id
    $body = $Text.Substring($span[0], $span[1] - $span[0])

    $encoded = if ($null -eq $Value) { 'null' } else { '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"' }
    $pattern = '"' + [regex]::Escape($Field) + '"\s*:\s*(?:"(?:[^"\\]|\\.)*"|null|true|false|-?\d+(?:\.\d+)?)'

    if ([regex]::IsMatch($body, $pattern)) {
        $newBody = [regex]::Replace($body, $pattern, '"' + $Field + '": ' + $encoded.Replace('$', '$$'), 1)
    }
    else {
        # Insere logo apos o "id", herdando a indentacao da linha seguinte.
        $mId = [regex]::Match($body, '"id"\s*:\s*"[^"]*",\r?\n(?<indent>[ \t]*)')
        if (-not $mId.Success) { throw "Nao consegui inserir '$Field' em '$Id'." }
        $indent = $mId.Groups['indent'].Value
        $insertAt = $mId.Index + $mId.Length
        $newBody = $body.Insert($insertAt, '"' + $Field + '": ' + $encoded + ",`n" + $indent)
    }

    $Text.Substring(0, $span[0]) + $newBody + $Text.Substring($span[1])
}

function Set-BacklogUpdatedAt([string]$Text, [string]$Date) {
    [regex]::Replace($Text, '("updatedAt"\s*:\s*)"[^"]*"', ('${1}"' + $Date + '"'), 1)
}

function Today { (Get-Date).ToString('yyyy-MM-dd') }

function Write-Field([string]$Label, $Value) {
    if ($null -eq $Value -or ($Value -is [string] -and $Value -eq '')) { $Value = '-' }
    Write-Host ("  {0,-16} {1}" -f ($Label + ':'), $Value)
}

function Write-Section([string]$Title) {
    Write-Host ''
    Write-Host $Title -ForegroundColor Cyan
}

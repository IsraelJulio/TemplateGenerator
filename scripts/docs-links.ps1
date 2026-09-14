<#
.SYNOPSIS
    Confere que todo link relativo entre documentos resolve para arquivo existente.

.DESCRIPTION
    Era verificacao manual em toda tarefa que mexe em documentacao (criterio de aceite de T00,
    item do checklist de paridade de docs/interop.md). E puramente mecanica.

    Confere links Markdown [texto](destino) com destino relativo, resolvendo ancoras (#secao)
    contra os cabecalhos do arquivo de destino quando ele e Markdown.

    Ignora http(s):, mailto:, e ancoras puras para o proprio arquivo quando o cabecalho existe.

.PARAMETER Path
    Raiz da varredura. Padrao: o repositorio inteiro (fora de .git, node_modules, bin, obj).
#>
[CmdletBinding()]
param([string]$Path)

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepoRoot
if (-not $Path) { $Path = $root }

$skip = '[\\/](\.git|node_modules|bin|obj|dist|\.angular|test-results|playwright-report)[\\/]'
$files = @(Get-ChildItem -Path $Path -Recurse -Filter '*.md' -File |
    Where-Object { $_.FullName -notmatch $skip })

$linkRe = [regex]'(?<!\!)\[(?<text>[^\]]*)\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)'
$headingRe = [regex]'(?m)^#{1,6}\s+(?<h>.+?)\s*$'

function ConvertTo-Anchor([string]$Heading) {
    $a = $Heading.ToLowerInvariant()
    $a = $a -replace '`', ''
    $a = $a -replace '[^\p{L}\p{Nd}\s-]', ''
    $a = $a.Trim() -replace '\s+', '-'
    $a
}

$anchorCache = @{}
function Get-Anchors([string]$FileAbs) {
    if ($anchorCache.ContainsKey($FileAbs)) { return $anchorCache[$FileAbs] }
    $set = New-Object System.Collections.Generic.HashSet[string]
    if (Test-Path $FileAbs) {
        $txt = [System.IO.File]::ReadAllText($FileAbs)
        foreach ($h in $headingRe.Matches($txt)) {
            [void]$set.Add((ConvertTo-Anchor $h.Groups['h'].Value))
        }
    }
    $anchorCache[$FileAbs] = $set
    $set
}

$broken = @()
$brokenAnchors = @()
$checked = 0

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($m in $linkRe.Matches($text)) {
        $target = $m.Groups['target'].Value
        if ($target -match '^(https?:|mailto:|data:|#)') {
            if ($target.StartsWith('#')) {
                $checked++
                $anchor = $target.TrimStart('#').ToLowerInvariant()
                if (-not (Get-Anchors $f.FullName).Contains($anchor)) {
                    $brokenAnchors += "$($f.FullName.Substring($root.Length + 1)) -> $target"
                }
            }
            continue
        }
        $checked++

        $parts = $target.Split('#', 2)
        $pathPart = [uri]::UnescapeDataString($parts[0])
        $anchorPart = if ($parts.Count -gt 1) { $parts[1].ToLowerInvariant() } else { $null }

        $resolved = Join-Path $f.DirectoryName $pathPart
        try { $resolved = [System.IO.Path]::GetFullPath($resolved) } catch { }

        if (-not (Test-Path $resolved)) {
            $broken += "$($f.FullName.Substring($root.Length + 1)) -> $target"
            continue
        }
        if ($anchorPart -and $resolved -match '\.md$') {
            if (-not (Get-Anchors $resolved).Contains($anchorPart)) {
                $brokenAnchors += "$($f.FullName.Substring($root.Length + 1)) -> $target"
            }
        }
    }
}

Write-Host "docs-links - $($files.Count) arquivos, $checked link(s) relativo(s)"

foreach ($b in $brokenAnchors) { Write-Host "  ANCORA $b" -ForegroundColor Yellow }
foreach ($b in $broken) { Write-Host "  QUEBRA $b" -ForegroundColor Red }

if ($broken.Count -eq 0 -and $brokenAnchors.Count -eq 0) {
    Write-Host '  OK     todo link relativo resolve, e toda ancora existe no destino.' -ForegroundColor Green
    exit 0
}
if ($broken.Count -eq 0) {
    Write-Host ''
    Write-Host "$($brokenAnchors.Count) ancora(s) sem cabecalho correspondente. Nenhum arquivo ausente." -ForegroundColor Yellow
    exit 1
}
Write-Host ''
Write-Host "$($broken.Count) link(s) quebrado(s), $($brokenAnchors.Count) ancora(s) invalida(s)." -ForegroundColor Red
exit 1

<#
.SYNOPSIS
    Roda as verificacoes padrao do repositorio, guarda a saida completa em arquivo e devolve
    ao terminal so o que serve de evidencia.

.DESCRIPTION
    Existe para resolver a tensao entre duas exigencias do projeto:
      - o relatorio precisa de evidencia real de execucao;
      - despejar milhares de linhas no contexto do agente e desperdicio e piora o raciocinio.

    A saida COMPLETA vai para um arquivo (o log e a evidencia arquivavel). Para o terminal vao
    o comando, o exit code, a duracao, as linhas de erro/falha e o resumo do runner.

    As `verifications` do backlog sao prosa, nao comandos: este script cobre os comandos padrao
    do repositorio. Verificacao especifica da tarefa (subir o ZIP extraido, exercitar CRUD,
    conferir hash) continua sendo trabalho do agente - e a evidencia dela entra no relatorio
    do mesmo jeito.

.PARAMETER Only
    Subconjunto a rodar: build, test, web, e2e. Aceita varios.

.PARAMETER LogDir
    Onde gravar os logs completos. Padrao: um diretorio temporario FORA do repositorio
    (arquivo temporario nunca e versionado - AGENTS.md).

.PARAMETER TailOnFailure
    Quantas linhas finais mostrar quando um passo falha. Padrao 40.
#>
[CmdletBinding()]
param(
    [ValidateSet('build', 'test', 'web', 'e2e')]
    [string[]]$Only,
    [string]$LogDir,
    [int]$TailOnFailure = 40
)

. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepoRoot
if (-not $LogDir) {
    $LogDir = Join-Path $env:TEMP "templategenerator-verify/$(Get-Date -Format 'yyyyMMdd-HHmmss')"
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$steps = @(
    @{ Key = 'build'; Name = 'dotnet build';  Exe = 'dotnet'; Args = @('build', 'TemplateGenerator.sln', '--nologo', '-v', 'minimal'); Cwd = $root }
    @{ Key = 'test';  Name = 'dotnet test';   Exe = 'dotnet'; Args = @('test', 'TemplateGenerator.sln', '--nologo', '-v', 'minimal'); Cwd = $root }
    @{ Key = 'web';   Name = 'npm run build'; Exe = 'npm';    Args = @('run', 'build');  Cwd = (Join-Path $root 'src/web') }
    @{ Key = 'e2e';   Name = 'npm run e2e';   Exe = 'npm';    Args = @('run', 'e2e');    Cwd = (Join-Path $root 'src/web') }
)

if ($Only) { $steps = @($steps | Where-Object { $Only -contains $_.Key }) }
else { $steps = @($steps | Where-Object { $_.Key -ne 'e2e' }) }   # e2e so sob pedido: e lento

# Linhas que valem a pena trazer ao contexto mesmo quando o passo passa.
$signal = '(?i)\b(error|erro)\b|\bwarn(ing)?\b|\bfail(ed|ures?)?\b|\bPassed!|\bFailed!|\bskipped\b|\bBuild succeeded\b|\bTest summary\b|\bexit code\b'
# Ruido que nunca ajuda.
$noise = '(?i)^\s*$|Determining projects to restore|Restored .*\.csproj|^\s*\d+ of \d+ projects'

$results = @()

foreach ($s in $steps) {
    $log = Join-Path $LogDir "$($s.Key).log"
    Write-Host ''
    Write-Host "$ $($s.Name)" -ForegroundColor White

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Push-Location $s.Cwd
    try {
        # 2>&1 junta stderr; Out-File guarda TUDO. Nada e perdido.
        & $s.Exe @($s.Args) 2>&1 | Out-File -FilePath $log -Encoding utf8
        $code = $LASTEXITCODE
    }
    catch {
        $_.Exception.Message | Out-File -FilePath $log -Encoding utf8 -Append
        $code = 127
    }
    finally { Pop-Location; $sw.Stop() }

    $all = @(Get-Content $log -ErrorAction SilentlyContinue)
    $interesting = @($all | Where-Object { $_ -match $signal -and $_ -notmatch $noise } | Select-Object -Unique)

    if ($code -ne 0) {
        # Falhou: o que importa sao as ultimas linhas e os erros.
        Write-Host "  exit code: $code" -ForegroundColor Red
        $errs = @($all | Where-Object { $_ -match '(?i)\b(error|erro)\b|\bfail(ed)?\b' } | Select-Object -First 15)
        foreach ($l in $errs) { Write-Host "  | $l" -ForegroundColor Red }
        if ($errs.Count -eq 0) {
            foreach ($l in @($all | Select-Object -Last $TailOnFailure)) { Write-Host "  | $l" -ForegroundColor DarkGray }
        }
    }
    else {
        Write-Host "  exit code: 0" -ForegroundColor Green
        foreach ($l in @($interesting | Select-Object -Last 12)) { Write-Host "  | $l" }
    }

    Write-Host "  $([math]::Round($sw.Elapsed.TotalSeconds, 1))s | $($all.Count) linhas | log completo: $log" -ForegroundColor DarkGray

    $results += [pscustomobject]@{
        Passo    = $s.Name
        ExitCode = $code
        Segundos = [math]::Round($sw.Elapsed.TotalSeconds, 1)
        Linhas   = $all.Count
        Log      = $log
    }
}

Write-Host ''
Write-Host 'Resumo' -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

$failed = @($results | Where-Object { $_.ExitCode -ne 0 })
Write-Host "Logs completos em: $LogDir" -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Para o relatorio, cole: o comando, o exit code e o trecho que comprova o criterio.' -ForegroundColor DarkGray
Write-Host 'Nao cole o log inteiro - docs/conventions.md#evidencia-em-relatorio.' -ForegroundColor DarkGray

if ($failed.Count -gt 0) {
    Write-Host ''
    Write-Host "$($failed.Count) passo(s) falharam: $(($failed | ForEach-Object { $_.Passo }) -join ', ')" -ForegroundColor Red
    exit 1
}
exit 0

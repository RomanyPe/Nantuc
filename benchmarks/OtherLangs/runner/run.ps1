<#
.SYNOPSIS
    Runner for OtherLangs benchmarks (scripting languages only).

.DESCRIPTION
    Reads ../langs.json, runs each available interpreter against its benchmark
    script, parses output in the format
        <Name>  <seconds> s  (result=<N>)
    and writes results.json / results.csv + prints a summary table.

.EXAMPLE
    .\run.ps1
    .\run.ps1 -Languages python,lua -Cases Call,Math -Repeat 3 -Verbose
#>
[CmdletBinding()]
param(
    [string[]]$Languages = @(),
    [string[]]$Cases     = @(),
    [string]  $Output    = "results.json",
    [int]     $Timeout   = 300,
    [int]     $Repeat    = 1
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir   = Split-Path -Parent $scriptDir
$langsFile = Join-Path $rootDir "langs.json"

if (-not (Test-Path $langsFile)) {
    Write-Error "langs.json not found at $langsFile"
    exit 1
}

$config = Get-Content $langsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$selected = @($config.languages)
if ($Languages.Count -gt 0) {
    $selected = $selected | Where-Object { $Languages -contains $_.id }
}

$resultRe = [regex]'^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+(?<time>\d+(?:\.\d+)?)\s*s\s+.*result=(?<result>-?\d+)'

function Get-InterpreterVersion {
    param([string]$Exe)
    $flag = switch ($Exe) {
        { $_ -in 'python','python3','py' } { '--version' }
        'lua'                              { '-v' }
        'luajit'                           { '-v' }
        'ruby'                             { '--version' }
        'node'                             { '--version' }
        default                            { $null }
    }
    if (-not $flag) { return $null }
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName               = $Exe
        $psi.Arguments              = $flag
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.UseShellExecute        = $false
        $psi.CreateNoWindow         = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $out = $p.StandardOutput.ReadToEnd()
        $err = $p.StandardError.ReadToEnd()
        $null = $p.WaitForExit(5000)
        $text = (($out + "`n" + $err).Trim() -split "`n")[0]
        return $text
    } catch { return $null }
}

function Invoke-BenchmarkProcess {
    param(
        [string]   $Exe,
        [string[]] $ArgList,
        [string]   $Cwd,
        [int]      $TimeoutSec
    )
    $tmpOut = [System.IO.Path]::GetTempFileName()
    $tmpErr = [System.IO.Path]::GetTempFileName()
    $timedOut = $false
    try {
        $argStr = ($ArgList -join ' ')
        $proc = Start-Process -FilePath $Exe -ArgumentList $argStr `
                    -WorkingDirectory $Cwd -NoNewWindow -PassThru `
                    -RedirectStandardOutput $tmpOut -RedirectStandardError $tmpErr
        if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
            try { $proc.Kill() } catch {}
            $timedOut = $true
        }
        $stdout = if (Test-Path $tmpOut) { Get-Content -Raw -Encoding UTF8 $tmpOut } else { "" }
        $stderr = if (Test-Path $tmpErr) { Get-Content -Raw -Encoding UTF8 $tmpErr } else { "" }
    } finally {
        Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue
    }
    return [PSCustomObject]@{
        Stdout   = $stdout
        Stderr   = $stderr
        TimedOut = $timedOut
        ExitCode = if ($proc) { $proc.ExitCode } else { -1 }
    }
}

function Parse-Output {
    param([string]$Text)
    $out = @()
    foreach ($line in ($Text -split "`r?`n")) {
        $m = $resultRe.Match($line)
        if ($m.Success) {
            $out += [PSCustomObject]@{
                case    = $m.Groups['name'].Value
                seconds = [double]::Parse($m.Groups['time'].Value,
                                          [System.Globalization.CultureInfo]::InvariantCulture)
                result  = [long]$m.Groups['result'].Value
            }
        }
    }
    return $out
}

# ── main loop ─────────────────────────────────────────────────────────────

$allResults = New-Object System.Collections.Generic.List[object]

foreach ($lang in $selected) {
    $exe  = $lang.run[0]
    $args_ = @($lang.run[1..($lang.run.Length - 1)])
    $cwd  = Join-Path $rootDir $lang.cwd

    Write-Host ""
    Write-Host "=== $($lang.name) [$($lang.id)] ===" -ForegroundColor Cyan
    Write-Host "    cmd: $exe $($args_ -join ' ')"
    Write-Host "    cwd: $cwd"

    $cmdInfo = Get-Command $exe -ErrorAction SilentlyContinue
    if (-not $cmdInfo) {
        Write-Warning "  '$exe' not found in PATH — skipping '$($lang.id)'"
        continue
    }
    $ver = Get-InterpreterVersion -Exe $exe
    if ($ver) { Write-Host "    version: $ver" -ForegroundColor DarkGray }

    $runs = @()
    for ($i = 1; $i -le $Repeat; $i++) {
        if ($Repeat -gt 1) { Write-Host "    run $i/$Repeat ..." -ForegroundColor DarkGray }
        $r = Invoke-BenchmarkProcess -Exe $exe -ArgList $args_ -Cwd $cwd -TimeoutSec $Timeout
        if ($r.TimedOut) {
            Write-Warning "  timed out after ${Timeout}s — skipping"
            $runs = @()
            break
        }
        $parsed = Parse-Output ($r.Stderr + "`n" + $r.Stdout)
        if ($parsed.Count -eq 0) {
            Write-Warning "  no results parsed (exit=$($r.ExitCode))"
            if ($VerbosePreference -ne 'SilentlyContinue') {
                Write-Host "  raw stderr:" -ForegroundColor DarkYellow
                ($r.Stderr -split "`r?`n" | Select-Object -First 20) |
                    ForEach-Object { Write-Host "    $_" }
            }
            $runs = @()
            break
        }
        $runs += ,$parsed
    }

    if ($runs.Count -eq 0) { continue }

    # take the minimum per case across repeats
    $flatten = $runs | ForEach-Object { $_ }
    $best = $flatten |
            Group-Object case |
            ForEach-Object {
                $_.Group | Sort-Object seconds | Select-Object -First 1
            }

    foreach ($p in $best) {
        if ($Cases.Count -gt 0 -and $Cases -notcontains $p.case) { continue }
        $allResults.Add([PSCustomObject]@{
            lang     = $lang.id
            language = $lang.name
            category = $lang.category
            case     = $p.case
            seconds  = [math]::Round($p.seconds, 6)
            result   = $p.result
        })
    }

    Write-Host "    parsed $($best.Count) cases" -ForegroundColor Green
    $best | Sort-Object seconds -Descending |
        Format-Table case, @{n='seconds';e={'{0,10:F4}' -f $_.seconds}}, result -AutoSize |
        Out-Host
}

if ($allResults.Count -eq 0) {
    Write-Warning "No results collected."
    exit 2
}

$allResults | Export-Csv -Path $Output -NoTypeInformation -Encoding UTF8
$jsonPath = [System.IO.Path]::ChangeExtension($Output, '.json')
($allResults | ConvertTo-Json -Depth 4) | Set-Content -Path $jsonPath -Encoding UTF8
Write-Host ""
Write-Host "Saved $($allResults.Count) results → $Output, $jsonPath" -ForegroundColor Green

# ── summary pivot ─────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=== Summary (seconds per case, min across runs) ===" -ForegroundColor Cyan
$cases   = @($allResults | Select-Object -ExpandProperty case   -Unique)
$langIds = @($allResults | Select-Object -ExpandProperty lang   -Unique)
$rows = foreach ($c in $cases) {
    $row = [ordered]@{ Case = $c }
    foreach ($l in $langIds) {
        $v = $allResults | Where-Object { $_.case -eq $c -and $_.lang -eq $l } |
                          Select-Object -First 1
        $row[$l] = if ($v) { '{0:F4}' -f $v.seconds } else { '-' }
    }
    [PSCustomObject]$row
}
$rows | Format-Table -AutoSize | Out-Host
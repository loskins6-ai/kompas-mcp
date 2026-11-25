# End-to-end controls for the JSONL runner and the geometry checks (KOMPAS v22).
param([string]$Server)
$ErrorActionPreference = 'Stop'
if (!$Server) { $Server = Join-Path (Split-Path $PSScriptRoot -Parent) 'KompasMcp.exe' }
$exe = Get-Item -LiteralPath $Server
$cases = (New-Item -ItemType Directory -Path (Join-Path $exe.DirectoryName ('mcp-out/smoke-controls-' + [guid]::NewGuid().ToString('N')))).FullName
$runner = Join-Path $PSScriptRoot 'run_smoke.ps1'
$scenario = Join-Path $PSScriptRoot 'smoke_3d.jsonl'

function Invoke-Case($Name, $File, $ExpectedCode, $ErrorPattern) {
    $output = Join-Path $cases $Name
    # Child PowerShell: exercise the exit code seen by CI and the documented command.
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner $File -Server $exe.FullName -OutputDirectory $output | Out-Host
    if ($LASTEXITCODE -ne $ExpectedCode) { throw "$Name returned $LASTEXITCODE, expected $ExpectedCode" }
    $report = Get-Content -LiteralPath (Join-Path $output 'report.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($report.passed -ne ($ExpectedCode -eq 0) -or ($ErrorPattern -and $report.error -notmatch $ErrorPattern)) {
        throw "$Name produced an unexpected report: $($report.error)"
    }
    return $report
}

$good = Invoke-Case 'positive' $scenario 0
if ($good.geometry.Count -ne 2 -or $good.geometry[0].cad_version[0] -ne 22 -or
    $good.geometry[0].walls.Count -ne 2 -or $good.geometry[1].walls.Count -ne 9) {
    throw 'Missing bushing or flange measurements'
}

$lines = Get-Content -LiteralPath $scenario -Encoding UTF8
$hole = @($lines | Where-Object { ($_ | ConvertFrom-Json).params.name -eq 'hole' })
if ($hole.Count -ne 1) { throw 'Expected one bushing hole operation' }
$utf8 = New-Object Text.UTF8Encoding($false)
$solid = Join-Path $cases 'solid.jsonl'
[IO.File]::WriteAllLines($solid, [string[]]($lines -cne $hole[0]), $utf8)
$null = Invoke-Case 'solid' $solid 1 'bush\.bore: expected .*measured \[\]'

$changed = $hole[0] | ConvertFrom-Json
$changed.params.arguments.r = 14
$bore28 = Join-Path $cases 'bore28.jsonl'
$wrong = foreach ($line in $lines) {
    if ($line -ceq $hole[0]) { $changed | ConvertTo-Json -Compress -Depth 10 } else { $line }
}
[IO.File]::WriteAllLines($bore28, [string[]]$wrong, $utf8)
$null = Invoke-Case 'bore28' $bore28 1 'bush\.bore\.diameter_mm: expected 30, measured \[28,28\]'

$blind = Join-Path $cases 'blind.jsonl'
$blindLines = foreach ($line in $lines) {
    $line
    if ($line -ceq $hole[0]) {
        # Fill the first 20 mm of the through bore, leaving the overall size unchanged.
        '{"id":"blind-sketch","method":"tools/call","params":{"name":"sketch","arguments":{"plane":"XOY","elements":[{"type":"circle","xc":0,"yc":0,"r":15}]}}}'
        '{"id":"blind-bottom","method":"tools/call","params":{"name":"extrude_boss","arguments":{"depth":20}}}'
    }
}
[IO.File]::WriteAllLines($blind, [string[]]$blindLines, $utf8)
$null = Invoke-Case 'blind' $blind 1 'bush\.bore: expected .*measured .*"center_mm":\[0,0,20\]'

$rpc = Join-Path $cases 'rpc.jsonl'
[IO.File]::WriteAllText($rpc, '{"id":1,"method":"unknown-method"}', $utf8)
$null = Invoke-Case 'rpc' $rpc 1 'RPC -32601'
$tool = Join-Path $cases 'tool.jsonl'
[IO.File]::WriteAllText($tool, '{"id":1,"method":"tools/call","params":{"name":"open_part","arguments":{}}}', $utf8)
$null = Invoke-Case 'tool' $tool 1 'open_part.*path'
Write-Host "ALL CONTROLS PASS: $cases"

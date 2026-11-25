param(
    [Parameter(Mandatory, Position=0)][string]$Scenario,
    [string]$OutputDirectory,
    [string]$Server,
    [ValidateRange(1,86400)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$runtime = @()
$process = $null
$writer = $null
$run = $null
$step = 'setup'
$verified = @()
$failure = $null
try {
    if (!$Server) { $Server = Join-Path (Split-Path $PSScriptRoot -Parent) 'KompasMcp.exe' }
    $exe = Get-Item -LiteralPath $Server
    if (!(Test-Path -LiteralPath $Scenario)) {
        $Scenario = Join-Path (Split-Path $PSScriptRoot -Parent) $Scenario
    }
    $inputFile = Get-Item -LiteralPath $Scenario
    if (!$OutputDirectory) {
        $OutputDirectory = Join-Path $exe.DirectoryName ('mcp-out/smoke-' + [guid]::NewGuid().ToString('N'))
    }
    # Create exclusively. KOMPAS resolves paths relative to the server EXE.
    $run = (New-Item -ItemType Directory -Path $OutputDirectory).FullName
    foreach ($name in @($exe.Name, 'Kompas6API5.dll', 'KompasAPI7.dll', 'Kompas6Constants.dll', 'Kompas6Constants3D.dll', 'KAPITypes.dll')) {
        $source = Join-Path $exe.DirectoryName $name
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $copy = Copy-Item -LiteralPath $source -Destination $run -PassThru
        $runtime += $copy.FullName
    }
    Write-Host "Smoke run: $run"
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $run $exe.Name
    $info.WorkingDirectory = $run
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.StandardOutputEncoding = $utf8
    $process = [Diagnostics.Process]::Start($info)
    $writer = New-Object IO.StreamWriter($process.StandardInput.BaseStream, $utf8)
    $writer.AutoFlush = $true

    $requests = 0
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($inputFile.FullName, $utf8)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $step = "line $lineNumber"
        $request = $line | ConvertFrom-Json
        if (!$request.method) { throw 'Missing request method' }
        $tool = $request.params.name
        $step = "line $lineNumber [$($request.id)] $($request.method) $tool"
        $writer.WriteLine($line)
        if ($null -eq $request.id) { continue }
        $requests++
        $read = $process.StandardOutput.ReadLineAsync()
        if (!$read.Wait(1000 * $TimeoutSeconds)) { throw 'Timed out waiting for response' }
        if ($null -eq $read.Result) { throw 'Server closed stdout before responding' }
        $reply = $read.Result | ConvertFrom-Json
        if ($reply.jsonrpc -ne '2.0' -or $null -eq $reply.id -or ($request.id -is [string]) -ne ($reply.id -is [string]) -or $request.id -cne $reply.id) {
            throw "Unexpected response id or version: $($reply.id), $($reply.jsonrpc)"
        }
        if ($null -ne $reply.error) { throw "RPC $($reply.error.code): $($reply.error.message)" }
        if ($null -eq $reply.result) { throw 'Missing result' }
        if ($request.method -eq 'tools/call') {
            if ($reply.result.isError -isnot [bool]) { throw 'Missing or invalid tool status' }
            $message = ($reply.result.content | ForEach-Object { $_.text }) -join ' '
            if ($reply.result.isError) { throw $message }
            if ($tool -eq 'check_round_part') { $verified += ($message | ConvertFrom-Json) }
            Write-Host "[$($request.id)] $tool $message"
        } else { Write-Host "[$($request.id)] $($request.method) OK" }
    }
    if (!$requests) { throw 'Scenario has no requests' }
    $step = 'shutdown'
    $writer.Close()
    $tail = $process.StandardOutput.ReadToEndAsync()
    if (!$process.WaitForExit(1000 * $TimeoutSeconds)) { throw 'Server did not exit' }
    if ($process.ExitCode -ne 0) { throw "Server exited with code $($process.ExitCode)" }
    if ($tail.Result.Trim()) { throw 'Unexpected response after last request' }
} catch {
    $failure = "${step}: " + $_.Exception.Message
    [Console]::Error.WriteLine("SMOKE FAILED: $failure")
} finally {
    try {
        if ($writer) { $writer.Dispose() }
    } finally {
        if ($process) {
            if (!$process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
        }
        foreach ($path in $runtime) { Remove-Item -LiteralPath $path -Force }
        if ($run) {
            $report = [ordered]@{ scenario = $inputFile.Name; passed = ($null -eq $failure); geometry = $verified; error = $failure }
            [IO.File]::WriteAllText((Join-Path $run 'report.json'), ($report | ConvertTo-Json -Depth 12), $utf8)
        }
    }
}
if ($failure) { exit 1 }
Write-Host "SMOKE PASS: $run"
exit 0

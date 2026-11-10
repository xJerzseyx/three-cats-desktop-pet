param([switch]$IncludeStartup)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testDirectory = Join-Path $repoRoot 'work'
New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null
Push-Location $repoRoot
try {
    & (Join-Path $repoRoot 'build.ps1') -OutputDirectory $testDirectory
    & $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$testDirectory/LogicTests.exe" /r:System.Drawing.dll (Join-Path $PSScriptRoot 'LogicTests.cs') (Join-Path $repoRoot 'src/Model.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Logic test compilation failed.' }
    & (Join-Path $testDirectory 'LogicTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Logic tests failed.' }
    & $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$testDirectory/AllowAudio.exe" (Join-Path $PSScriptRoot 'AudioTone.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Audio fixture compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $testDirectory 'AllowAudio.exe') -Destination (Join-Path $testDirectory 'IgnoreAudio.exe')

    $tonePath = Join-Path $testDirectory 'test-tone.wav'
    $writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create($tonePath))
    try {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
        $writer.Write([int]88236)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $writer.Write([int]16)
        $writer.Write([short]1)
        $writer.Write([short]1)
        $writer.Write([int]44100)
        $writer.Write([int]88200)
        $writer.Write([short]2)
        $writer.Write([short]16)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
        $writer.Write([int]88200)
        for ($sample = 0; $sample -lt 44100; $sample++) {
            $amplitude = 0.25 + 0.75 * [Math]::Pow([Math]::Sin(2 * [Math]::PI * 2 * $sample / 44100), 8)
            $writer.Write([short](900 * [Math]::Sin(2 * [Math]::PI * 440 * $sample / 44100) * $amplitude))
        }
    } finally { $writer.Dispose() }

    & $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$testDirectory/IntegrationTests.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$testDirectory/伊二三万.exe" (Join-Path $PSScriptRoot 'IntegrationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
    & (Join-Path $testDirectory 'IntegrationTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }

    if ($IncludeStartup) {
        & $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$testDirectory/StartupTests.exe" (Join-Path $PSScriptRoot 'StartupTests.cs') (Join-Path $repoRoot 'src/StartupRegistration.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Startup test compilation failed.' }
        & (Join-Path $testDirectory 'StartupTests.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Startup tests failed.' }
    }
} finally { Pop-Location }

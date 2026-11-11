param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 C# compiler was not found.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$sourceFiles = @('Model.cs', 'AudioMonitor.cs', 'SpriteRenderer.cs', 'Native.cs', 'StartupRegistration.cs', 'CursorScheme.cs', 'CursorSession.cs', 'PlayerDialog.cs', 'CatWindow.cs', 'PetForm.cs', 'Program.cs')
$sourcePaths = $sourceFiles | ForEach-Object { Join-Path (Join-Path $PSScriptRoot 'src') $_ }
$assetPath = Join-Path $PSScriptRoot 'assets/cats.png'
$cursorPath = Join-Path $PSScriptRoot 'assets/cursors.zip'
$manifestPath = Join-Path $PSScriptRoot 'src/app.manifest'
$executablePath = Join-Path $OutputDirectory '伊二三万.exe'
& $compiler /nologo /utf8output /target:winexe /platform:x64 /optimize+ /unsafe- "/out:$executablePath" "/win32manifest:$manifestPath" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll /r:System.IO.Compression.dll "/resource:$cursorPath,ThreeCats.Cursors.zip" "/resource:$assetPath,ThreeCats.Cats.png" @sourcePaths
if ($LASTEXITCODE -ne 0) { throw 'EXE build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'USAGE.zh-CN.txt') -Destination (Join-Path $OutputDirectory '使用说明.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $OutputDirectory 'LICENSE.txt')
Write-Output "Built $executablePath"

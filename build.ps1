$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sourceFile = Join-Path $taskRoot 'ChatGPTTextSize.cs'
$binaryFile = Join-Path $taskRoot 'ChatGPTTextSize.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$binaryFile" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $sourceFile
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-FileHash -Algorithm SHA256 -LiteralPath "$taskRoot/ChatGPTTextSize.exe"

$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $pluginRoot 'bin\CodexConversationNavigator.exe'
$source = Join-Path $pluginRoot 'src\Program.cs'
$build = Join-Path $PSScriptRoot 'build.ps1'

if (-not (Test-Path -LiteralPath $executable) -or
    (Get-Item -LiteralPath $source).LastWriteTimeUtc -gt (Get-Item -LiteralPath $executable).LastWriteTimeUtc) {
    & $build | Out-Null
}

Start-Process -FilePath $executable -WindowStyle Hidden
Write-Output 'Codex Conversation Navigator started.'

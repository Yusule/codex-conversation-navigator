$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $pluginRoot 'src\Program.cs'
$binPath = Join-Path $pluginRoot 'bin'
$outputPath = Join-Path $binPath 'CodexConversationNavigator.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows C# compiler was not found.'
}
if (-not (Test-Path -LiteralPath $references)) {
    throw '.NET Framework 4.8 reference assemblies were not found.'
}

New-Item -ItemType Directory -Force -Path $binPath | Out-Null

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    "/out:$outputPath",
    "/reference:$references\WindowsBase.dll",
    "/reference:$references\PresentationCore.dll",
    "/reference:$references\PresentationFramework.dll",
    "/reference:$references\System.Xaml.dll",
    "/reference:$references\UIAutomationClient.dll",
    "/reference:$references\UIAutomationTypes.dll",
    $sourcePath
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Output $outputPath

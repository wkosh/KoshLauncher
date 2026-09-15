param([Parameter(Mandatory=$true)][string]$InputDirectory,
      [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$inputPath = [IO.Path]::GetFullPath($InputDirectory)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
function Escape-Xml([string]$value) { [Security.SecurityElement]::Escape($value) }
$config = @"
<Obfuscator>
 <Var name="InPath" value="$(Escape-Xml $inputPath)" />
 <Var name="OutPath" value="$(Escape-Xml $outputPath)" />
 <Var name="KeepPublicApi" value="false" />
 <Var name="HidePrivateApi" value="true" />
 <Var name="HideStrings" value="true" />
 <Var name="UseUnicodeNames" value="true" />
 <Var name="RenameProperties" value="true" />
 <Var name="RenameEvents" value="true" />
 <Var name="RenameFields" value="true" />
 <Var name="RegenerateDebugInfo" value="false" />
 <Var name="XmlMapping" value="true" />
 <AssemblySearchPath path="$(Escape-Xml $inputPath)" />
 <Module file="$(Escape-Xml (Join-Path $inputPath 'KoshLauncher.dll'))">
  <!-- WPF locates these root types through compiled markup. Methods remain eligible. -->
  <SkipType name="KoshLauncher.App" />
  <SkipType name="KoshLauncher.MainWindow" />
  <!-- JSON property names are an existing on-disk contract. -->
  <SkipType name="KoshLauncher.LauncherSettings" skipProperties="true" />
 </Module>
</Obfuscator>
"@
$configPath = Join-Path $outputPath 'obfuscar.xml'
[IO.File]::WriteAllText($configPath, $config)
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & dotnet tool run obfuscar.console $configPath
    if ($LASTEXITCODE -ne 0) { throw 'Obfuscation failed; publication stopped.' }
    if (!(Test-Path -LiteralPath (Join-Path $outputPath 'KoshLauncher.dll'))) { throw 'Protected assembly missing.' }
} finally { Pop-Location }

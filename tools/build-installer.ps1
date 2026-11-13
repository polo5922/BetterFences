# Publie BetterFences (autonome, .NET inclus) puis compile l'installateur Inno Setup dans dist\.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'src\BetterFences\BetterFences.csproj'
$publish = Join-Path $root 'publish'

$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
# ReadyToRun : code précompilé, démarrage nettement plus rapide (moins de compilation JIT).
dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish a échoué' }

$iscc = @(
  (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 introuvable (https://jrsoftware.org/isdl.php)' }

& $iscc "/DAppVersion=$version" (Join-Path $root 'installer\BetterFences.iss')
if ($LASTEXITCODE -ne 0) { throw 'La compilation de l''installateur a échoué' }
Get-ChildItem (Join-Path $root 'dist') -Filter *.exe | Select-Object Name, @{n='Mo';e={[math]::Round($_.Length/1MB,1)}}

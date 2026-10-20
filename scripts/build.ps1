# Build on Windows (PowerShell 5+/7, .NET SDK 8). Usage: .\scripts\build.ps1 [-Version 1.1.0]
param([string]$Version = "")
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$csproj = "src/ZiwuMeridianClock/ZiwuMeridianClock.csproj"
if (-not $Version) { $Version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
$Out = Join-Path $Root "dist"; $Pkg = Join-Path $Out "ZiwuMeridianClock"
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Pkg | Out-Null
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o (Join-Path $Out "publish")
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Copy-Item (Join-Path $Out "publish/ZiwuMeridianClock.exe") $Pkg
Copy-Item web (Join-Path $Pkg "web") -Recurse
Copy-Item "src/ZiwuMeridianClock/使用说明.txt","src/ZiwuMeridianClock/README_Lively.txt","LICENSE","THIRD_PARTY_NOTICES.md" $Pkg
Copy-Item licenses (Join-Path $Pkg "licenses") -Recurse
$zip = Join-Path $Out "ZiwuMeridianClock-v$Version-win-x64.zip"
$web = Join-Path $Out "ZiwuMeridianClock-v$Version-web.zip"
Compress-Archive -Path $Pkg -DestinationPath $zip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $Pkg "web") -DestinationPath $web -CompressionLevel Optimal
Get-FileHash $zip, $web -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLower(), (Split-Path $_.Path -Leaf) } | Set-Content (Join-Path $Out "SHA256SUMS.txt") -Encoding ascii
Get-Content (Join-Path $Out "SHA256SUMS.txt")
Write-Host "done: $Out"

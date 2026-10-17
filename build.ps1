# SalsaNeural portable build — publishes a self-contained win-x64 folder and zips it.
# Run from the repository root on Windows:
#   powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = "Stop"

$Src  = "RenoDXCommander"
$Out  = Join-Path $PSScriptRoot "dist\SalsaNeural"
$Zip  = Join-Path $PSScriptRoot "dist\SalsaNeural-Portable.zip"

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# Self-contained: bundles .NET + Windows App SDK — no runtime install needed on GFN.
dotnet publish (Join-Path $Src "RenoDXCommander.csproj") `
    -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:Platform=x64 `
    --self-contained true `
    -o $Out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Drop helper (asInvoker; only used if someone runs elevated on a normal PC)
dotnet build "RHI.DropHelper\RHI.DropHelper.csproj" -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw "DropHelper build failed" }
Copy-Item "RHI.DropHelper\bin\x64\Release\net8.0-windows\RHI.DropHelper.exe" $Out -Force
Copy-Item "RHI.DropHelper\bin\x64\Release\net8.0-windows\RHI.DropHelper.dll" $Out -Force -ErrorAction SilentlyContinue
Copy-Item "RHI.DropHelper\bin\x64\Release\net8.0-windows\RHI.DropHelper.runtimeconfig.json" $Out -Force -ErrorAction SilentlyContinue

# Content files the app expects beside the EXE
$Content = @(
    "icon.ico", "7z.exe", "7z.dll", "ReShade.ini", "ReShade.Vulkan.ini", "ReShade64.json",
    "RHI_PatchNotes.md", "relimiter.ini", "DisplayCommander.ini", "dxvk.conf",
    "OptiScaler.nvidia.ini", "OptiScaler.amd-dlss.ini", "OptiScaler.amd-nodlss.ini",
    "OptiScaler_nightly.nvidia.ini", "OptiScaler_nightly.amd-dlss.ini", "OptiScaler_nightly.amd-nodlss.ini",
    "OptiScaler_dlssnr.nvidia.ini", "OptiScaler_dlssnr.amd-dlss.ini", "OptiScaler_dlssnr.amd-nodlss.ini",
    "OptiScaler_f5.nvidia.ini", "OptiScaler_f5.amd-dlss.ini", "OptiScaler_f5.amd-nodlss.ini"
)
foreach ($f in $Content) {
    $p = Join-Path $Src $f
    if (Test-Path $p) { Copy-Item $p $Out -Force }
}

New-Item -ItemType Directory -Force -Path (Join-Path $Out "Assets\icons") | Out-Null
Copy-Item (Join-Path $Src "Assets\icons\*.ico") (Join-Path $Out "Assets\icons") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $Src "Assets\icons\*.png") (Join-Path $Out "Assets\icons") -Force -ErrorAction SilentlyContinue

# ClrMD freeze diagnostics (loose beside EXE)
$clrmd = "$env:USERPROFILE\.nuget\packages\microsoft.diagnostics.runtime\3.1.512801\lib\net6.0\Microsoft.Diagnostics.Runtime.dll"
if (Test-Path $clrmd) { Copy-Item $clrmd $Out -Force }

# Portable zip
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $PSScriptRoot "dist\SalsaNeural") -DestinationPath $Zip -Force

Write-Host "Portable build: $Zip"

param(
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    if ($Clean) { 
        if (Test-Path ./bin) { Remove-Item -Recurse -Force ./bin }
        if (Test-Path ./obj) { Remove-Item -Recurse -Force ./obj }
    }

    dotnet restore ./VismaSoftwareNordic.csproj
    # Publish as framework-dependent single-file (requires .NET Desktop Runtime on target)
    dotnet publish ./VismaSoftwareNordic.csproj -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -p:DebugType=none
    $outDir = Join-Path (Resolve-Path .).Path 'bin/Release/net6.0-windows/win-x64/publish'
    $exe = Join-Path $outDir 'VismaSoftwareNordic.exe'
    if (!(Test-Path $exe)) { throw "Publish output not found: $exe" }

    $dist = Join-Path (Resolve-Path ..).Path 'dist'
    if (Test-Path $dist) {
        Get-ChildItem $dist -File | Remove-Item -Force
    } else {
        New-Item -ItemType Directory -Path $dist | Out-Null
    }

    $scr = Join-Path $dist 'VismaSoftwareNordic.scr'
    Copy-Item $exe $scr -Force
    
    Write-Host "Created $scr"
}
finally {
    Pop-Location
}

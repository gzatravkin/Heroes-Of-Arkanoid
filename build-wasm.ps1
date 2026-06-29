# build-wasm.ps1 — full static build: WASM publish + copy _framework → frontend/public/ + optional Vite build
#
# NOTE: frontend/public/_framework/ is gitignored (large binary WASM bundle).
# Run this script once before 'npm run dev' or 'npm run build' in the frontend.
#
# Requirements:
#   dotnet workload install wasm-tools-net8
#
# Usage:
#   pwsh build-wasm.ps1            # full build: WASM + Vite
#   pwsh build-wasm.ps1 -SkipVite  # WASM publish + _framework copy only (then use npm run dev)

param([switch]$SkipVite)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==> Publishing Arkanoid.Wasm (browser-wasm)..." -ForegroundColor Cyan
# WasmAppDir redirects the browser AppBundle (containing _framework/) to wasm-dist/AppBundle/
# so the Vite config and this script have a stable, predictable path to copy from.
dotnet publish backend/Arkanoid.Wasm -c Release `
    -p:WasmAppDir="$PSScriptRoot/wasm-dist/AppBundle" `
    --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed"; exit 1 }

$frameworkSrc = "$PSScriptRoot/wasm-dist/AppBundle/_framework"
if (-not (Test-Path $frameworkSrc)) {
    Write-Error "_framework not found at: $frameworkSrc (dotnet publish may have failed silently)"
    exit 1
}

Write-Host "==> Copying _framework → frontend/public/_framework..." -ForegroundColor Cyan
$dest = "$PSScriptRoot/frontend/public/_framework"
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item $frameworkSrc $dest -Recurse

Write-Host "==> _framework ready at: $dest" -ForegroundColor Gray
Write-Host "    (source: $frameworkSrc)" -ForegroundColor Gray

if (-not $SkipVite) {
    Write-Host "==> Building Vite frontend..." -ForegroundColor Cyan
    Set-Location "$PSScriptRoot/frontend"
    npm run build
    if ($LASTEXITCODE -ne 0) { Write-Error "Vite build failed"; exit 1 }
    Set-Location $PSScriptRoot
    Write-Host "==> Done. Static site ready at: frontend/dist/" -ForegroundColor Green
} else {
    Write-Host "==> Skipped Vite build (-SkipVite flag). Run 'npm run dev' in frontend/ to start dev server." -ForegroundColor Yellow
}

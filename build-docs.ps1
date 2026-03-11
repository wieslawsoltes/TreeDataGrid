$ErrorActionPreference = 'Stop'

function Clear-DocsOutputs {
    Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter 'Avalonia.Controls.TreeDataGrid.api.json' -Recurse -File |
        Where-Object { $_.FullName.Replace('\', '/') -like '*/obj/Release/*' } |
        Remove-Item -Force

    $apiCache = Join-Path $PSScriptRoot 'site/.lunet/build/cache/api/dotnet'
    $wwwRoot = Join-Path $PSScriptRoot 'site/.lunet/build/www'
    foreach ($path in @($apiCache, $wwwRoot)) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Push-Location $PSScriptRoot
try {
    $lockDir = Join-Path $PSScriptRoot 'site/.lunet/.build-lock'
    while ($true) {
        if (Test-Path $lockDir) {
            Start-Sleep -Seconds 1
            continue
        }

        try {
            New-Item -ItemType Directory -Path $lockDir -ErrorAction Stop | Out-Null
            break
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    dotnet tool restore
    Clear-DocsOutputs

    Push-Location site
    try {
        $lunetLog = [System.IO.Path]::GetTempFileName()
        try {
            dotnet tool run lunet --stacktrace build 2>&1 | Tee-Object -FilePath $lunetLog

            $lunetErrors = rg -n 'ERR lunet|Error while building api dotnet|Unable to select the api dotnet output' $lunetLog
            if ($LASTEXITCODE -eq 0 -and $lunetErrors) {
                throw "Lunet reported API/site build errors.`n$lunetErrors"
            }
        }
        finally {
            Remove-Item $lunetLog -Force -ErrorAction SilentlyContinue
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item (Join-Path $PSScriptRoot 'site/.lunet/.build-lock') -Force -Recurse -ErrorAction SilentlyContinue
    Pop-Location
}

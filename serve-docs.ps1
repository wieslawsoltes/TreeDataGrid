$ErrorActionPreference = 'Stop'

$hostAddress = if ($env:DOCS_HOST) { $env:DOCS_HOST } else { '127.0.0.1' }
$port = if ($env:DOCS_PORT) { $env:DOCS_PORT } else { '8080' }

function Clear-ServeDocsOutputs {
    Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter 'Avalonia.Controls.TreeDataGrid.api.json' -Recurse -File |
        Where-Object { $_.FullName.Replace('\', '/') -like '*/obj/Release/*' } |
        Remove-Item -Force

    $apiCache = Join-Path $PSScriptRoot 'site/.lunet/build/cache/api/dotnet'
    $wwwRoot = Join-Path $PSScriptRoot 'site/.lunet/build/www'
    foreach ($path in @($apiCache, $wwwRoot)) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-AvailablePort {
    param(
        [string]$HostAddress,
        [int]$StartPort
    )

    $port = $StartPort
    $ipAddress = [System.Net.IPAddress]::Loopback
    [System.Net.IPAddress]::TryParse($HostAddress, [ref]$ipAddress) | Out-Null

    while ($true) {
        $listener = [System.Net.Sockets.TcpListener]::new($ipAddress, $port)

        try {
            $listener.Start()
            $listener.Stop()
            return $port
        }
        catch {
            try {
                $listener.Stop()
            }
            catch {
            }

            $port++
        }
    }
}

dotnet tool restore
Clear-ServeDocsOutputs
Push-Location site
try {
    if (Get-Command python3 -ErrorAction SilentlyContinue) {
        $pythonCommand = 'python3'
        $pythonArgs = @('-m', 'http.server', $port, '--bind', $hostAddress)
    } elseif (Get-Command python -ErrorAction SilentlyContinue) {
        $pythonCommand = 'python'
        $pythonArgs = @('-m', 'http.server', $port, '--bind', $hostAddress)
    } elseif (Get-Command py -ErrorAction SilentlyContinue) {
        $pythonCommand = 'py'
        $pythonArgs = @('-3', '-m', 'http.server', $port, '--bind', $hostAddress)
    } else {
        Write-Warning "Python runtime not found (python3/python/py). Falling back to 'lunet serve'."
        dotnet tool run lunet --stacktrace serve
        return
    }

    $port = Get-AvailablePort -HostAddress $hostAddress -StartPort ([int]$port)

    dotnet tool run lunet --stacktrace build --dev

    $watcher = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('tool', 'run', 'lunet', '--stacktrace', 'build', '--dev', '--watch') `
        -NoNewWindow `
        -PassThru

    try {
        Write-Host "Serving docs at http://${hostAddress}:$port"
        Write-Host 'Watching docs with Lunet (dev mode)...'

        Push-Location '.lunet/build/www'
        try {
            & $pythonCommand @pythonArgs
        }
        finally {
            Pop-Location
        }
    }
    finally {
        if ($watcher -and -not $watcher.HasExited) {
            Stop-Process -Id $watcher.Id -Force
        }
    }
}
finally {
    Pop-Location
}

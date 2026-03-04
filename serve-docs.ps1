$ErrorActionPreference = 'Stop'

$hostAddress = if ($env:DOCS_HOST) { $env:DOCS_HOST } else { '127.0.0.1' }
$port = if ($env:DOCS_PORT) { $env:DOCS_PORT } else { '8080' }

dotnet tool restore
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

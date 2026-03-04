$ErrorActionPreference = 'Stop'

dotnet tool restore
Push-Location site
try {
    dotnet tool run lunet --stacktrace build
}
finally {
    Pop-Location
}

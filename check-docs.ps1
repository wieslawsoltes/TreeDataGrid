$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build-docs.ps1')

$docRoot = Join-Path $PSScriptRoot 'site/.lunet/build/www'

$requiredFiles = @(
    (Join-Path $docRoot 'index.html'),
    (Join-Path $docRoot 'api/index.html'),
    (Join-Path $docRoot 'articles/index.html'),
    (Join-Path $docRoot 'articles/getting-started/index.html'),
    (Join-Path $docRoot 'articles/concepts/index.html'),
    (Join-Path $docRoot 'articles/guides/index.html'),
    (Join-Path $docRoot 'articles/xaml/index.html'),
    (Join-Path $docRoot 'articles/advanced/index.html'),
    (Join-Path $docRoot 'articles/reference/index.html'),
    (Join-Path $docRoot 'articles/getting-started/overview/index.html'),
    (Join-Path $docRoot 'articles/getting-started/installation/index.html'),
    (Join-Path $docRoot 'articles/getting-started/quickstart-flat/index.html'),
    (Join-Path $docRoot 'articles/getting-started/quickstart-hierarchical/index.html'),
    (Join-Path $docRoot 'articles/guides/troubleshooting/index.html'),
    (Join-Path $docRoot 'articles/xaml/samples-walkthrough/index.html'),
    (Join-Path $docRoot 'articles/advanced/diagnostics-and-testing/index.html'),
    (Join-Path $docRoot 'articles/reference/package-and-assembly/index.html'),
    (Join-Path $docRoot 'articles/reference/api-coverage-index/index.html'),
    (Join-Path $docRoot 'articles/reference/lunet-docs-pipeline/index.html'),
    (Join-Path $docRoot 'articles/reference/license/index.html'),
    (Join-Path $docRoot 'articles/build-and-package/index.html'),
    (Join-Path $docRoot 'articles/samples/index.html'),
    (Join-Path $docRoot 'css/lite.css')
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        throw "Required docs output missing: $file"
    }
}

$rawMarkdownLinks = rg -nP 'href="(?!https?://)[^"]*\.md(?:[?#"][^"]*)?"' $docRoot
if ($LASTEXITCODE -eq 0 -and $rawMarkdownLinks) {
    throw "Generated docs contain raw .md links.`n$rawMarkdownLinks"
}

$readmeRoutes = rg -n 'href="[^"]*/readme(?:[?#"][^"]*)?"' $docRoot
if ($LASTEXITCODE -eq 0 -and $readmeRoutes) {
    throw "Generated docs contain /readme routes instead of directory routes.`n$readmeRoutes"
}

$staleApiRoutes = rg -n 'href="[^"]*/api/index\.md(?:[?#"][^"]*)?"' $docRoot
if ($LASTEXITCODE -eq 0 -and $staleApiRoutes) {
    throw "Generated docs contain stale /api/index.md links.`n$staleApiRoutes"
}

$rawMarkdownOutputs = Get-ChildItem -Path (Join-Path $docRoot 'articles') -Filter *.md -Recurse -ErrorAction SilentlyContinue
if ($rawMarkdownOutputs.Count -gt 0) {
    $paths = ($rawMarkdownOutputs | ForEach-Object { $_.FullName }) -join "`n"
    throw "Generated docs still contain raw .md article outputs.`n$paths"
}

$badFooterText = rg -n 'Creative Commons|CC BY 2.5' (Join-Path $docRoot 'index.html') (Join-Path $docRoot 'articles/getting-started/overview/index.html')
if ($LASTEXITCODE -eq 0 -and $badFooterText) {
    throw "Generated docs contain the default Creative Commons footer instead of the project MIT license footer.`n$badFooterText"
}

$missingMitFooter = rg -F 'MIT license' (Join-Path $docRoot 'index.html')
if ($LASTEXITCODE -ne 0) {
    throw 'Generated site footer is missing the project MIT license text.'
}

$treeDataGridApiPage = Join-Path $docRoot 'api/Avalonia.Controls.TreeDataGrid/index.html'
if (-not (Test-Path $treeDataGridApiPage)) {
    throw "Expected TreeDataGrid API page is missing: $treeDataGridApiPage"
}

$missingAvaloniaLink = rg -F 'https://api-docs.avaloniaui.net/docs/Avalonia.Controls.Control/' $treeDataGridApiPage
if ($LASTEXITCODE -ne 0) {
    throw 'Generated TreeDataGrid API page is missing the external Avalonia.Controls.Control link.'
}

$xamlIndexPage = Join-Path $docRoot 'articles/xaml/index.html'
$missingBasepathCss = rg -F '/TreeDataGrid/css/lite.css' $xamlIndexPage
if ($LASTEXITCODE -ne 0) {
    throw 'Production XAML docs page is missing the project-basepath-prefixed lite.css URL.'
}

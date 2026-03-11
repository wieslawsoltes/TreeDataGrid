#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"${SCRIPT_DIR}/build-docs.sh"

DOC_ROOT="${SCRIPT_DIR}/site/.lunet/build/www"

test -f "${DOC_ROOT}/index.html"
test -f "${DOC_ROOT}/api/index.html"
test -f "${DOC_ROOT}/articles/index.html"
test -f "${DOC_ROOT}/articles/getting-started/index.html"
test -f "${DOC_ROOT}/articles/concepts/index.html"
test -f "${DOC_ROOT}/articles/guides/index.html"
test -f "${DOC_ROOT}/articles/xaml/index.html"
test -f "${DOC_ROOT}/articles/advanced/index.html"
test -f "${DOC_ROOT}/articles/reference/index.html"
test -f "${DOC_ROOT}/articles/getting-started/overview/index.html"
test -f "${DOC_ROOT}/articles/getting-started/installation/index.html"
test -f "${DOC_ROOT}/articles/getting-started/quickstart-flat/index.html"
test -f "${DOC_ROOT}/articles/getting-started/quickstart-hierarchical/index.html"
test -f "${DOC_ROOT}/articles/guides/troubleshooting/index.html"
test -f "${DOC_ROOT}/articles/xaml/samples-walkthrough/index.html"
test -f "${DOC_ROOT}/articles/advanced/diagnostics-and-testing/index.html"
test -f "${DOC_ROOT}/articles/reference/package-and-assembly/index.html"
test -f "${DOC_ROOT}/articles/reference/api-coverage-index/index.html"
test -f "${DOC_ROOT}/articles/reference/lunet-docs-pipeline/index.html"
test -f "${DOC_ROOT}/articles/reference/license/index.html"
test -f "${DOC_ROOT}/articles/build-and-package/index.html"
test -f "${DOC_ROOT}/articles/samples/index.html"
test -f "${DOC_ROOT}/css/lite.css"

if rg -nP 'href="(?!https?://)[^"]*\.md(?:[?#"][^"]*)?"' "${DOC_ROOT}" >/dev/null; then
    echo "Generated docs contain raw .md links."
    exit 1
fi

if rg -n 'href="[^"]*/readme(?:[?#"][^"]*)?"' "${DOC_ROOT}" >/dev/null; then
    echo "Generated docs contain /readme routes instead of directory routes."
    exit 1
fi

if rg -n 'href="[^"]*/api/index\.md(?:[?#"][^"]*)?"' "${DOC_ROOT}" >/dev/null; then
    echo "Generated docs contain stale /api/index.md links."
    exit 1
fi

if find "${DOC_ROOT}/articles" -name '*.md' -print -quit | grep -q .; then
    echo "Generated docs still contain raw .md article outputs."
    find "${DOC_ROOT}/articles" -name '*.md' -print
    exit 1
fi

if rg -n 'Creative Commons|CC BY 2.5' "${DOC_ROOT}/index.html" "${DOC_ROOT}/articles/getting-started/overview/index.html" >/dev/null; then
    echo "Generated docs contain the default Creative Commons footer instead of the project MIT license footer."
    exit 1
fi

if ! rg -F 'MIT license' "${DOC_ROOT}/index.html" >/dev/null; then
    echo "Generated site footer is missing the project MIT license text."
    exit 1
fi

TREE_DATAGRID_API_PAGE="${DOC_ROOT}/api/Avalonia.Controls.TreeDataGrid/index.html"
if ! test -f "${TREE_DATAGRID_API_PAGE}"; then
    echo "Expected TreeDataGrid API page is missing: ${TREE_DATAGRID_API_PAGE}"
    exit 1
fi

if ! rg -F 'https://api-docs.avaloniaui.net/docs/Avalonia.Controls.Control/' "${TREE_DATAGRID_API_PAGE}" >/dev/null; then
    echo "Generated TreeDataGrid API page is missing the external Avalonia.Controls.Control link."
    exit 1
fi

XAML_INDEX_PAGE="${DOC_ROOT}/articles/xaml/index.html"
if ! rg -F '/TreeDataGrid/css/lite.css' "${XAML_INDEX_PAGE}" >/dev/null; then
    echo "Production XAML docs page is missing the project-basepath-prefixed lite.css URL."
    exit 1
fi

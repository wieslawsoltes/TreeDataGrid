#!/bin/bash
set -euo pipefail

dotnet tool restore
cd site
dotnet tool run lunet --stacktrace build

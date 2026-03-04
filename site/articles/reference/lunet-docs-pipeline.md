---
title: "Lunet Docs Pipeline"
---

# Lunet Docs Pipeline

This repository migrated from DocFX to Lunet on `master`.

## Scope of migration

- Full content migration from `docfx/articles/**` to `site/articles/**`
- API docs migration from DocFX metadata to Lunet `api.dotnet`
- Docs scripts and GitHub Actions migration to Lunet build output
- Removal of DocFX configs and generated-doc assumptions

## Site structure

- `site/config.scriban`: Lunet config and API docs setup
- `site/menu.yml`: Top-level nav (Home, Articles, API)
- `site/articles/**`: Main documentation pages
- `site/articles/**/menu.yml`: Section sidebars
- `site/images/**`: Images used by documentation
- `site/.lunet/css/template-main.css`: precompiled template stylesheet (runtime Sass workaround)
- `site/.lunet/css/site-overrides.css`: project-specific UI/UX customizations

## API generation

The API reference is generated from:

- `../src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj`

via `with api.dotnet` in `config.scriban`.

Current API settings:

- `TargetFramework: net8.0`
- local API pages generated under `/api`
- `external_apis` mappings for Avalonia assemblies to `https://api-docs.avaloniaui.net/docs`

This keeps TreeDataGrid APIs local while external Avalonia xrefs resolve to the official Avalonia API site.

## Styling pipeline note

Lunet `1.0.10` on macOS 15 has a Dart Sass platform detection issue.
To keep the full template visual quality:

- docs pages are assigned `bundle: "lite"` via `with attributes`
- a local `/_builtins/bundle.sbn-html` override resolves bundle links safely
- `template-main.css` is precompiled and committed, then loaded by the `lite` bundle

To refresh `template-main.css` locally after template updates:

```bash
npx --yes sass --no-source-map --style=expanded \
  --load-path site/.lunet/build/cache/.lunet/resources/npm/bootstrap/5.3.8/scss \
  --load-path site/.lunet/build/cache/.lunet/resources/npm/bootstrap-icons/1.13.1/font \
  site/.lunet/build/cache/.lunet/extends/github/lunet-io/templates/main/dist/.lunet/css/main.scss \
  site/.lunet/css/template-main.css
```

## Commands

From repository root:

```bash
./build-docs.sh
./check-docs.sh
./serve-docs.sh
```

`serve-docs.sh` prints the local URL and auto-selects the next free port when `DOCS_PORT` is already in use.

PowerShell:

```powershell
./build-docs.ps1
./serve-docs.ps1
```

All commands run Lunet in `site/` and output to `site/.lunet/build/www`.

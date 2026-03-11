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
- `src/Avalonia.Controls.TreeDataGrid/apidocs/**`: Markdown merged into generated namespace/type API pages

## API generation

The API reference is generated from:

- `../src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj`

via `with api.dotnet` in `config.scriban`.

Current API settings:

- `TargetFramework: net8.0`
- local API pages generated under `/api`
- package id advertised by the docs site: `TreeDataGrid`
- `external_apis` mappings for Avalonia assemblies to `https://api-docs.avaloniaui.net/docs`

This keeps TreeDataGrid APIs local while external Avalonia xrefs resolve to the official Avalonia API site.

Namespace and selected type summaries are merged from `src/Avalonia.Controls.TreeDataGrid/apidocs/**` so the generated API pages can carry project-specific narrative text without modifying the Lunet templates.

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

`build-docs.sh`/`build-docs.ps1` now clean stale Lunet output and cached `Avalonia.Controls.TreeDataGrid.api.json` artifacts before rebuilding, so API/docs output always comes from the current source tree instead of prior build residue.

`check-docs.sh`/`check-docs.ps1` build the site and then verify:

- required section and compatibility routes exist
- generated output contains no raw `.md` links or `/readme` routes
- reference pages do not point at the stale `api/index.md` path
- the footer uses the project MIT license instead of the template Creative Commons footer
- the generated TreeDataGrid API page keeps its external Avalonia type links

`serve-docs.sh` and `serve-docs.ps1` print the local URL, auto-select the next free port when `DOCS_PORT` is already in use, and start from a clean docs output directory before entering watch mode.

PowerShell:

```powershell
./build-docs.ps1
./check-docs.ps1
./serve-docs.ps1
```

GitHub Actions now use `check-docs.sh` both for PR validation and for the publish workflow, so broken docs links/routes fail before deployment.

All commands run Lunet in `site/` and output to `site/.lunet/build/www`.

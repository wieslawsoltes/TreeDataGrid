---
name: treedatagrid-for-avalonia-usage
description: Implement, troubleshoot, and review TreeDataGrid usage in this repository by grounding work in DocFX articles (`docfx/articles/**`) and generated API YAML (`docfx/api/**`). Use when tasks involve TreeDataGrid setup, flat or hierarchical sources, columns/cells/rows, selection, editing, drag and drop, XAML themes/templates, performance/primitives internals, or public API lookup.
---

# TreeDataGrid for Avalonia Usage

## Quick Start

- Run preflight and export docs root (repo-local skill path):

```bash
eval "$(python3 skills/treedatagrid-for-avalonia-usage/scripts/ensure_docfx_docs.py --print-export)"
```

- If the skill is installed in `$CODEX_HOME`, run:

```bash
eval "$(python3 "${CODEX_HOME:-$HOME/.codex}/skills/treedatagrid-for-avalonia-usage/scripts/ensure_docfx_docs.py" --print-export)"
```

- Route the request with [`references/docfx-navigation.md`](references/docfx-navigation.md).
- Read narrative guidance from `$TREE_DATAGRID_DOCS_ROOT/docfx/articles/**` before editing code.
- Resolve member-level behavior from `$TREE_DATAGRID_DOCS_ROOT/docfx/api/*.yml` before finalizing implementation details.
- Include exact article paths and API UIDs in the final response.

## Workflow

### 0. Ensure DocFX docs are available

- Run `scripts/ensure_docfx_docs.py` before opening docs.
- Preflight behavior:
- Reuse local docs when `docfx/articles/toc.yml` and `docfx/api/toc.yml` already exist.
- Otherwise sparse-clone docs and source into `$CODEX_HOME/cache/treedatagrid-docfx`, then generate API YAML when needed.
- Export `TREE_DATAGRID_DOCS_ROOT` for all subsequent article/API lookups.
- Pin docs with `TREE_DATAGRID_DOCS_REF=<branch|tag|sha>` when deterministic versioning is required.
- Preflight requires `git` and `docfx` (or `dotnet tool run docfx`) when API YAML generation is needed.

### 1. Route Request to the Right Article Set

- Map the user request to one or more article groups:
- `getting-started/` for onboarding and first implementation.
- `guides/` for feature work (sources, columns, selection, editing, drag and drop, styling).
- `xaml/` for `ControlTheme`, template keys, and resource customization.
- `advanced/` for internals, performance, element factories, and typed binding.
- `reference/` for type-to-article mapping and namespace entry pages.

### 2. Read Canonical Narrative Docs First

- Open the primary article for the task.
- Open the related conceptual page when the task touches indices, rows, columns, or selection semantics.
- Open the matching namespace reference page from `docfx/articles/reference/`.
- Avoid legacy stubs whose title is `Article Moved`; use the destination pages under structured folders.

### 3. Confirm API Contracts from Generated YAML

- Start with `docfx/articles/reference/api-coverage-index.md` to map a type to its primary article.
- Traverse `docfx/api/toc.yml` to confirm namespace and symbol names.
- Inspect `docfx/api/<type>.yml` for signatures, members, and inherited APIs.
- Run (repo-local skill path):

```bash
python3 skills/treedatagrid-for-avalonia-usage/scripts/find_docfx_api.py <uid-or-fragment>
```

- If the skill is installed in `$CODEX_HOME`, run:

```bash
python3 "${CODEX_HOME:-$HOME/.codex}/skills/treedatagrid-for-avalonia-usage/scripts/find_docfx_api.py" <uid-or-fragment>
```

- Use `--exact` when the UID is known and deterministic lookup is required.
- Use `--include-toc` only when you intentionally want hits from `docfx/api/toc.yml`.
- For generic UIDs that contain backticks, quote the argument:

```bash
python3 "${CODEX_HOME:-$HOME/.codex}/skills/treedatagrid-for-avalonia-usage/scripts/find_docfx_api.py" 'Avalonia.Controls.Models.TreeDataGrid.TextColumn`2' --exact
```

### 4. Implement and Verify

- Align code changes with the documented behavior from articles and API pages.
- Validate with targeted tests for behavior changes.
- Build docs if article references or links are edited.
- If docs and source diverge, inspect source code and call out the mismatch explicitly.

### 5. Report Evidence

- Report the exact files used as evidence:
- article paths from `docfx/articles/**`
- API paths from `docfx/api/*.yml` or explicit UIDs
- commands/tests executed and outcomes

## High-Value Entry Points

- Paths below are relative to `$TREE_DATAGRID_DOCS_ROOT`.
- `docfx/articles/toc.yml`
- `docfx/articles/reference/api-coverage-index.md`
- `docfx/articles/reference/namespace-avalonia-controls.md`
- `docfx/articles/reference/namespace-models-treedatagrid.md`
- `docfx/articles/reference/namespace-selection.md`
- `docfx/articles/reference/namespace-primitives.md`
- `docfx/api/toc.yml`
- `references/docfx-navigation.md`

## Resources

### scripts/

- `scripts/ensure_docfx_docs.py`: Ensure TreeDataGrid docs exist locally; sparse-fetch required paths into cache and generate API YAML when needed.
- `scripts/find_docfx_api.py`: Query `docfx/api/*.yml` for exact and fuzzy UID matches with file/line output.

### references/

- `references/docfx-navigation.md`: Route user tasks to the canonical DocFX article and API entry points quickly.

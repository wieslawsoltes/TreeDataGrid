---
title: "Namespace: Avalonia.Experimental.Data / Avalonia.Experimental.Data.Core"
---

# Namespace: Avalonia.Experimental.Data / Avalonia.Experimental.Data.Core

This section documents the typed-binding public APIs used by TreeDataGrid column internals and advanced scenarios.

## Functional Groups

The groups below map to the typed-binding pipeline stages.

### Binding Factories and Definitions

- [`TypedBinding<TIn>`](xref:Avalonia.Experimental.Data.TypedBinding`1)
- [`TypedBinding<TIn,TOut>`](xref:Avalonia.Experimental.Data.TypedBinding`2)

### Instantiated Binding Expressions

- [`TypedBindingExpression<TIn,TOut>`](xref:Avalonia.Experimental.Data.Core.TypedBindingExpression`2)

### Observable Infrastructure

- [`LightweightObservableBase<T>`](xref:Avalonia.Experimental.Data.Core.LightweightObservableBase`1)

### Expression Parsing

- [`ExpressionChainVisitor<TIn>`](xref:Avalonia.Data.Core.Parsers.ExpressionChainVisitor`1)

## Guidance

These APIs are advanced and mostly intended for framework-level integration.

Use standard column constructors first. Move to typed-binding APIs when you need explicit delegate-level control over read/write and change tracking behavior.

## Complete Type Index

| Type | Kind | Primary Article |
|---|---|---|
| <xref:Avalonia.Data.Core.Parsers.ExpressionChainVisitor`1> | Class | [advanced/typed-binding.md](../advanced/typed-binding.md) |
| <xref:Avalonia.Experimental.Data.Core.LightweightObservableBase`1> | Class | [advanced/typed-binding.md](../advanced/typed-binding.md) |
| <xref:Avalonia.Experimental.Data.Core.TypedBindingExpression`2> | Class | [advanced/typed-binding.md](../advanced/typed-binding.md) |
| <xref:Avalonia.Experimental.Data.TypedBinding`1> | Class | [advanced/typed-binding.md](../advanced/typed-binding.md) |
| <xref:Avalonia.Experimental.Data.TypedBinding`2> | Class | [advanced/typed-binding.md](../advanced/typed-binding.md) |

## Related

- [Typed Binding](../advanced/typed-binding.md)
- [Column Text](../guides/column-text.md)
- [Column Template](../guides/column-template.md)
- [API Coverage Index](api-coverage-index.md)

# Typed Binding

TreeDataGrid exposes typed-binding APIs for strongly typed read/write delegates and lower boxing overhead than untyped bindings.

## Public Typed-Binding Surface

- [`TypedBinding<TIn>`](xref:Avalonia.Experimental.Data.TypedBinding`1): factory methods (`OneWay`, `TwoWay`, `Default`, `OneTime`).
- [`TypedBinding<TIn,TOut>`](xref:Avalonia.Experimental.Data.TypedBinding`2): binding definition and bind/instance methods.
- [`TypedBindingExpression<TIn,TOut>`](xref:Avalonia.Experimental.Data.Core.TypedBindingExpression`2): instantiated observable expression.
- [`ExpressionChainVisitor<TIn>`](xref:Avalonia.Data.Core.Parsers.ExpressionChainVisitor`1): expression-chain link extraction.
- [`LightweightObservableBase<T>`](xref:Avalonia.Experimental.Data.Core.LightweightObservableBase`1): low-allocation observable base used by expression pipeline.

## Typical Usage in Columns

TreeDataGrid column bases use typed bindings to connect model properties to cell models and edits.

```csharp
using Avalonia.Experimental.Data;

var oneWay = TypedBinding<Person>.OneWay(x => x.Name);
var twoWay = TypedBinding<Person>.TwoWay(x => x.Name);
```

You can also provide explicit read/write delegates:

```csharp
var custom = TypedBinding<Person>.TwoWay(
    read: x => x.Name,
    write: (x, v) => x.Name = v ?? string.Empty);
```

## Expression Chain Behavior

`ExpressionChainVisitor<TIn>.Build(...)` extracts intermediate objects from the lambda chain, for example from `x => x.Address.City.Name`.

The chain is used to subscribe weakly to relevant property/collection changes and republish current binding value.

## Binding Modes and Write Requirement

In [`TypedBinding<TIn,TOut>`](xref:Avalonia.Experimental.Data.TypedBinding`2):

- `OneWay` requires `Read` and `Links`
- `TwoWay` also requires `Write`
- `Default` resolves from target property metadata

If two-way mode is requested without `Write`, binding creation fails by design.

## Caveats

- APIs are in `Avalonia.Experimental.Data*`; treat as advanced and version-sensitive.
- `OneWayToSource` and `OneTime` paths are not fully implemented for all targets in this package.
- Complex expression chains should be profiled under high-frequency updates.

## Troubleshooting

- Binding returns fallback unexpectedly
Cause: null root or broken expression chain path.

- Two-way updates do nothing
Cause: `Write` delegate is missing or binding mode is not two-way.

- Too many change notifications
Cause: expression chain includes frequently changing collections/objects.

## API Coverage Checklist

- <xref:Avalonia.Data.Core.Parsers.ExpressionChainVisitor`1>
- <xref:Avalonia.Experimental.Data.Core.LightweightObservableBase`1>
- <xref:Avalonia.Experimental.Data.Core.TypedBindingExpression`2>
- <xref:Avalonia.Experimental.Data.TypedBinding`1>
- <xref:Avalonia.Experimental.Data.TypedBinding`2>

## Related

- [Column Text](../guides/column-text.md)
- [Column Template](../guides/column-template.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
- [Namespace: Avalonia.Experimental.Data / Avalonia.Experimental.Data.Core](../reference/namespace-experimental.md)

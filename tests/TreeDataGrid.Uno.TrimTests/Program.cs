using System;
using System.Collections.Generic;
using System.Globalization;
using Uno.Controls;
using Uno.Controls.Presentation;

if (BindingFeatures.ReflectionEnabled) throw new InvalidOperationException("The trimmed test must not use the reflection fallback.");
TreeDataGridBindingRegistry.RegisterProperty<Model, List<Child>>(nameof(Model.Children), static row => row.Children);
TreeDataGridBindingRegistry.RegisterProperty<Model, Guid>(nameof(Model.Id), static row => row.Id, static (row, value) => row.Id = value);
TreeDataGridBindingRegistry.RegisterProperty<Model, Amount>(nameof(Model.Amount), static row => row.Amount, static (row, value) => row.Amount = value);
TreeDataGridBindingRegistry.RegisterProperty<Child, bool?>(nameof(Child.Flag), static row => row.Flag, static (row, value) => row.Flag = value);
TreeDataGridBindingRegistry.RegisterIndexer<List<Child>, int, Child>(static (row, key) => row[key], static (row, key, value) => row[key] = value);
TreeDataGridBindingRegistry.RegisterConversion<Amount>(static (value, culture) => new(decimal.Parse((string)value!, culture)));
TreeDataGridBindingRegistry.RegisterCollection<List<Model>, Model>();
var model = new Model();
model.Children.Add(new());
NativeBindingPathWriter.WritePath("Children[0].Flag", model, "true", CultureInfo.InvariantCulture);
if (model.Children[0].Flag != true) throw new InvalidOperationException("Typed nested write failed.");
NativeBindingPathWriter.WritePath("Children[0].Flag", model, "", CultureInfo.InvariantCulture);
if (model.Children[0].Flag is not null) throw new InvalidOperationException("Nullable conversion failed.");
var id = Guid.NewGuid();
NativeBindingPathWriter.WritePath("Id", model, id.ToString(), CultureInfo.InvariantCulture);
if (model.Id != id) throw new InvalidOperationException("Guid conversion failed.");
NativeBindingPathWriter.WritePath("Amount", model, "12,5", CultureInfo.GetCultureInfo("pl-PL"));
if (model.Amount.Value != 12.5m) throw new InvalidOperationException("Registered conversion failed.");
if (NativeBindingPathWriter.GetPathValueType(typeof(Model), "Children[0].Flag") != typeof(bool?))
    throw new InvalidOperationException("Indexed type discovery failed.");
if (TreeDataGridBindingRegistry.FindCollectionItemType(typeof(List<Model>)) != typeof(Model))
    throw new InvalidOperationException("Empty collection type discovery failed.");
Console.WriteLine("UNO_TRIMMED_BINDING_PASSED: typed nested/indexed writes, nullable/Guid/culture conversion, declared types; reflection fallback disabled");

internal sealed class Model
{
    public List<Child> Children { get; } = new();
    public Guid Id { get; set; }
    public Amount Amount { get; set; }
}
internal sealed class Child { public bool? Flag { get; set; } }
internal readonly record struct Amount(decimal Value);

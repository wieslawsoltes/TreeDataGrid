using System;
using System.Collections.Generic;
using System.Globalization;
using Uno.Controls.Presentation;
using Uno.UI.DataBinding;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

[CollectionDefinition("Native binding metadata", DisableParallelization = true)]
public sealed class NativeBindingMetadataCollection { }

[Collection("Native binding metadata")]
public sealed class NativeBindingMetadataTests : IDisposable
{
    private readonly IBindableMetadataProvider? _previous = BindableMetadata.Provider;
    private readonly Provider _provider;
    public NativeBindingMetadataTests()
    {
        _provider = new Provider(_previous);
        BindableMetadata.Provider = _provider;
    }
    public void Dispose() => BindableMetadata.Provider = _previous;

    [Fact]
    public void Generated_only_nested_properties_write_without_CLR_property_reflection()
    {
        var target = new Payload { Child = new Payload() };
        Write("GeneratedChild.GeneratedNumber", target, "12.5");
        Assert.Equal(12.5m, target.Child.Number);
        Assert.True(_provider.Lookups >= 2);
        Assert.Null(typeof(Payload).GetProperty("GeneratedNumber"));
    }

    [Fact]
    public void Generated_readonly_property_does_not_fall_back_to_a_CLR_setter()
    {
        var target = new Payload();
        Assert.Throws<InvalidOperationException>(() => Write("ReadOnly", target, "changed"));
        Assert.Equal("original", target.ReadOnly);
    }

    [Fact]
    public void Generated_setter_failure_and_declared_conversion_are_preserved()
    {
        var target = new Payload();
        Assert.Same(target.Failure, Assert.Throws<InvalidOperationException>(() => Write("GeneratedNumber", target, "-1")));
        Write("GeneratedNumber", target, "42.5");
        Assert.Equal(42.5m, target.Number);
    }

    [Fact]
    public void Metadata_provider_replacement_is_seen_without_a_global_endpoint_cache()
    {
        var target = new Payload();
        Write("GeneratedNumber", target, "1");
        var replacement = new Provider(_previous) { Offset = 10 };
        BindableMetadata.Provider = replacement;
        Write("GeneratedNumber", target, "2");
        Assert.Equal(12m, target.Number);
        Assert.True(replacement.Lookups > 0);
    }

    [Fact]
    public void Retired_write_is_cancelled_after_a_generated_getter_callback()
    {
        var current = true;
        var target = new Payload { Child = new Payload(), OnRead = () => current = false };
        Assert.Throws<OperationCanceledException>(() => NativeBindingPathWriter.WritePath(
            "GeneratedChild.GeneratedNumber", target, "99", CultureInfo.InvariantCulture, isCurrent: () => current));
        Assert.Equal(0m, target.Child.Number);
    }

    private static void Write(string path, object model, object? value) =>
        NativeBindingPathWriter.WritePath(path, model, value, CultureInfo.InvariantCulture);
    [Theory]
    [InlineData("[name]", "name")]
    [InlineData("[\" space key \"]", " space key ")]
    [InlineData("[17]", "17")]
    [InlineData("[\"17\"]", "17")]
    public void Generated_only_indexer_preserves_native_key_semantics(string path, string key)
    {
        var target = new IndexedPayload();
        Write(path, target, "written");
        Assert.Equal("written", target.Values[key]);
        Assert.Null(typeof(IndexedPayload).GetProperty("Item"));
    }

    [Fact]
    public void Generated_indexer_can_traverse_to_a_generated_property()
    {
        var child = new Payload();
        var target = new IndexedPayload();
        target.Values["child"] = child;
        Write("[child].GeneratedNumber", target, "7.5");
        Assert.Equal(7.5m, child.Number);
    }

    [Fact]
    public void Generated_indexer_retirement_prevents_the_nested_write()
    {
        var current = true;
        var child = new Payload();
        var target = new IndexedPayload { OnRead = () => current = false };
        target.Values["child"] = child;
        Assert.Throws<OperationCanceledException>(() => NativeBindingPathWriter.WritePath(
            "[child].GeneratedNumber", target, "9", CultureInfo.InvariantCulture, isCurrent: () => current));
        Assert.Equal(0m, child.Number);
    }

    [Fact]
    public void Generated_indexer_preserves_setter_failure_and_allows_retry()
    {
        var target = new IndexedPayload();
        Assert.Same(target.Failure, Assert.Throws<InvalidOperationException>(() => Write("[key]", target, "reject")));
        Assert.Empty(target.Values);
        Write("[key]", target, "retry");
        Assert.Equal("retry", target.Values["key"]);
    }

    [Fact]
    public void Generated_readonly_indexer_does_not_use_an_available_CLR_setter()
    {
        var target = new ReadOnlyIndexedPayload();
        Assert.Throws<InvalidOperationException>(() => Write("[key]", target, "changed"));
        Assert.Equal(0, target.Writes);
    }

    [Fact]
    public void Generated_string_indexer_uses_declared_conversion_without_writing_the_int_overload()
    {
        var target = new TypedIndexedPayload();
        Write("[17]", target, "12.5");
        Assert.Equal(12.5m, target.Number);
        Assert.Equal("17", target.Key);
        Assert.Equal(0, target.IntegerWrites);
    }

    private sealed class IndexedPayload
    {
        internal readonly Dictionary<string, object?> Values = new();
        internal Action? OnRead;
        internal readonly InvalidOperationException Failure = new("Generated indexer rejected value.");
    }
    private sealed class ReadOnlyIndexedPayload
    {
        internal int Writes;
        public string this[string key] { get => key; set => ++Writes; }
    }
    private sealed class TypedIndexedPayload
    {
        internal decimal Number;
        internal string? Key;
        internal int IntegerWrites;
        public decimal this[string key] { get => Number; set { Key = key; Number = value; } }
        public decimal this[int key] { get => -1; set => ++IntegerWrites; }
    }
    private sealed class Payload
    {
        internal Payload? Child;
        internal decimal Number;
        internal Action? OnRead;
        internal InvalidOperationException Failure { get; } = new("Generated setter rejected value.");
        public string ReadOnly { get; set; } = "original";
    }
    private sealed class Provider : IBindableMetadataProvider
    {
        private readonly IBindableMetadataProvider? _previous;
        private readonly BindableType _payload = new(3, typeof(Payload));
        private readonly BindableType _indexed = new(0, typeof(IndexedPayload));
        private readonly BindableType _readOnlyIndexed = new(0, typeof(ReadOnlyIndexedPayload));
        private readonly BindableType _typedIndexed = new(0, typeof(TypedIndexedPayload));
        public int Lookups;
        public decimal Offset;
        public Provider(IBindableMetadataProvider? previous)
        {
            _previous = previous;
            _indexed.AddIndexer((owner, key) =>
            {
                var payload = (IndexedPayload)owner;
                payload.OnRead?.Invoke();
                return payload.Values[key];
            }, (owner, key, value) =>
            {
                var payload = (IndexedPayload)owner;
                if (Equals(value, "reject")) throw payload.Failure;
                payload.Values[key] = value;
            });
            _readOnlyIndexed.AddIndexer((owner, key) => ((ReadOnlyIndexedPayload)owner)[key], null!);
            _typedIndexed.AddIndexer((owner, key) => ((TypedIndexedPayload)owner)[key],
                (owner, key, value) => ((TypedIndexedPayload)owner)[key] = (decimal)value!);
            _payload.AddProperty<Payload>("GeneratedChild", (owner, _) =>
            {
                var payload = (Payload)owner;
                payload.OnRead?.Invoke();
                return payload.Child!;
            });
            _payload.AddProperty<decimal>("GeneratedNumber", (owner, _) => ((Payload)owner).Number, (owner, value, _) =>
            {
                var payload = (Payload)owner;
                var number = (decimal)value!;
                if (number < 0) throw payload.Failure;
                payload.Number = number + Offset;
            });
            _payload.AddProperty<string>("ReadOnly", (owner, _) => ((Payload)owner).ReadOnly);
        }
        public IBindableType GetBindableTypeByType(Type type)
        {
            ++Lookups;
            return type == typeof(Payload) ? _payload : type == typeof(IndexedPayload) ? _indexed :
                type == typeof(ReadOnlyIndexedPayload) ? _readOnlyIndexed : type == typeof(TypedIndexedPayload) ? _typedIndexed :
                _previous?.GetBindableTypeByType(type)!;
        }
        public IBindableType GetBindableTypeByFullName(string fullName) =>
            fullName == typeof(Payload).FullName ? _payload : fullName == typeof(IndexedPayload).FullName ? _indexed :
                fullName == typeof(ReadOnlyIndexedPayload).FullName ? _readOnlyIndexed : fullName == typeof(TypedIndexedPayload).FullName ? _typedIndexed :
                _previous?.GetBindableTypeByFullName(fullName)!;
    }
}

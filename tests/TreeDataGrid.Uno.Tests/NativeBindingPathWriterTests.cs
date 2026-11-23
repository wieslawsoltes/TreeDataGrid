using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.UI.Xaml.Data;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class NativeBindingPathWriterTests
{
    [Fact]
    public void Overloads_follow_numeric_or_string_tokens_not_reflection_order()
    {
        var target = new Overloads();
        Write("[7]", target, "integer");
        Assert.Equal("int:7:integer", target.LastWrite);
        Write("[\"7\"]", target, "string");
        Assert.Equal("string:7:string", target.LastWrite);
        Write("[name]", target, "named");
        Assert.Equal("string:name:named", target.LastWrite);
    }

    [Fact]
    public void String_indexer_preserves_key_whitespace_and_single_quotes()
    {
        var target = new Dictionary<string, string>();
        Write("[ key ]", target, "spaced");
        Write("['key']", target, "quoted");
        Write("[\"key\"]", target, "plain");
        Assert.Equal("spaced", target[" key "]);
        Assert.Equal("quoted", target["'key'"]);
        Assert.Equal("plain", target["key"]);
    }

    [Fact]
    public void Missing_exact_overload_uses_a_single_convertible_endpoint()
    {
        var target = new Dictionary<long, decimal>();
        Write("[7]", target, "12.5");
        Assert.Equal(12.5m, target[7]);
        var strings = new Dictionary<string, string>();
        Write("[7]", strings, "string");
        Assert.Equal("string", strings["7"]);
    }

    [Fact]
    public void Ambiguous_fallback_is_rejected_before_any_mutation()
    {
        var target = new AmbiguousIndexers();
        Assert.Throws<AmbiguousMatchException>(() => Write("[7]", target, "value"));
        Assert.Equal(0, target.Writes);
    }

    [Theory]
    [InlineData(".Name")]
    [InlineData("Name.")]
    [InlineData("Target..Name")]
    [InlineData("Target[0")]
    [InlineData("Target[0]Name")]
    [InlineData("Target[\"unterminated]")]
    [InlineData("Target[a.b]")]
    [InlineData("Target[[0]]")]
    [InlineData("Target]Name")]
    public void Malformed_paths_fail_before_getters_or_setters_run(string path)
    {
        var target = new Model();
        Assert.Throws<ArgumentException>(() => Write(path, target, "changed"));
        Assert.Equal(0, target.Reads);
        Assert.Equal("original", target.Name);
    }

    [Fact]
    public void Nested_arrays_and_indexers_write_the_current_reference_owner()
    {
        var first = new Model();
        var second = new Model();
        var target = new[] { first, second };
        Write("[1].Name", target, "changed");
        Assert.Equal("original", first.Name);
        Assert.Equal("changed", second.Name);
        var lists = new[] { new List<Model> { first } };
        Write("[0][0].Name", lists, "nested");
        Assert.Equal("nested", first.Name);
    }

    [Fact]
    public void Setter_errors_preserve_the_original_exception_and_value()
    {
        var target = new Model();
        var error = Assert.Throws<InvalidOperationException>(() => Write("Validated", target, "invalid"));
        Assert.Same(target.Failure, error);
        Write("Validated", target, "valid");
        Assert.Equal("valid", target.Validated);
    }

    [Fact]
    public void Readonly_and_copied_struct_endpoints_cannot_report_success()
    {
        var target = new Model();
        Assert.Throws<InvalidOperationException>(() => Write("ReadOnly", target, "changed"));
        Assert.Throws<InvalidOperationException>(() => Write("Struct.Name", target, "changed"));
        Assert.Null(target.Struct.Name);
    }

    [Fact]
    public void Declared_endpoint_type_drives_culture_nullable_and_enum_conversion()
    {
        var target = new Model();
        NativeBindingPathWriter.WritePath("Number", target, "12,5", CultureInfo.GetCultureInfo("pl-PL"));
        Assert.Equal(12.5m, target.Number);
        Write("Number", target, "");
        Assert.Null(target.Number);
        Write("Choice", target, "friday");
        Assert.Equal(DayOfWeek.Friday, target.Choice);
    }

    [Fact]
    public void Public_setter_does_not_allow_reading_a_private_intermediate_getter()
    {
        Assert.Throws<InvalidOperationException>(() => Write("Hidden.Name", new Model(), "changed"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conversion_cannot_write_a_replaced_nested_owner(bool removeOwner)
    {
        var original = new Model();
        var replacement = new Model();
        var target = new Model?[] { original };
        var converter = new CallbackConverter(() => target[0] = removeOwner ? null : replacement);

        Assert.Throws<OperationCanceledException>(() => NativeBindingPathWriter.WritePath(
            "[0].Name", target, "changed", CultureInfo.InvariantCulture, converter));

        Assert.Equal("original", original.Name);
        Assert.Equal("original", replacement.Name);
        target[0] = replacement;
        Write("[0].Name", target, "retry");
        Assert.Equal("retry", replacement.Name);
    }

    private sealed class CallbackConverter(Action callback) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => value;
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            callback();
            return value;
        }
    }

    private static void Write(string path, object model, object? value) =>
        NativeBindingPathWriter.WritePath(path, model, value, CultureInfo.InvariantCulture);
    private sealed class Overloads
    {
        public string? LastWrite { get; private set; }
        public string this[string key] { get => ""; set => LastWrite = $"string:{key}:{value}"; }
        public string this[int key] { get => ""; set => LastWrite = $"int:{key}:{value}"; }
    }
    private sealed class AmbiguousIndexers
    {
        public int Writes { get; private set; }
        public string this[long key] { get => ""; set => ++Writes; }
        public string this[decimal key] { get => ""; set => ++Writes; }
    }
    private struct ValueOwner { public string? Name { get; set; } }
    private sealed class Model
    {
        public string Name { get; set; } = "original";
        public int Reads { get; private set; }
        public object Target { get { ++Reads; return this; } }
        public string ReadOnly => "original";
        public ValueOwner Struct { get; set; }
        public Model? Hidden { private get; set; }
        public decimal? Number { get; set; }
        public DayOfWeek Choice { get; set; }
        public InvalidOperationException Failure { get; } = new("Setter rejected the edit.");
        private string _validated = "original";
        public string Validated { get => _validated; set { if (value == "invalid") throw Failure; _validated = value; } }
    }
}

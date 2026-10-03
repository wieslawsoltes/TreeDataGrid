using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace TreeDataGrid.Parity.Tests;

public sealed class DeclaredSpecializedCellParityTests
{
    [Theory]
    [InlineData("TreeDataGridTextCell", "Realize")]
    [InlineData("TreeDataGridTextCell", "Unrealize")]
    [InlineData("TreeDataGridTextCell", "OnModelPropertyChanged")]
    [InlineData("TreeDataGridCheckBoxCell", "Realize")]
    [InlineData("TreeDataGridCheckBoxCell", "Unrealize")]
    [InlineData("TreeDataGridCheckBoxCell", "OnModelPropertyChanged")]
    [InlineData("TreeDataGridTemplateCell", "Realize")]
    public void Specialized_native_control_declares_the_reference_virtual_entry_point(string typeName, string member)
    {
        var expectedType = typeof(Avalonia.Controls.Primitives.TreeDataGridCell).Assembly.GetType("Avalonia.Controls.Primitives." + typeName)!;
        var actualType = typeof(Uno.Controls.Primitives.TreeDataGridCell).Assembly.GetType("Uno.Controls.Primitives." + typeName)!;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var expected = Assert.Single(expectedType.GetMethods(flags), m => m.Name == member);
        var actual = Assert.Single(actualType.GetMethods(flags), m => m.Name == member && m.GetParameters().Length == expected.GetParameters().Length);
        Assert.Equal(expected.IsPublic, actual.IsPublic);
        Assert.Equal(expected.IsFamily, actual.IsFamily);
        Assert.Equal(expected.IsVirtual, actual.IsVirtual);
        Assert.Equal(expected.IsFinal, actual.IsFinal);
        Assert.Equal(expected.ReturnType, actual.ReturnType);
        Assert.Equal(actualType, actual.DeclaringType);
        Assert.NotEqual(actual.DeclaringType, actual.GetBaseDefinition().DeclaringType);
        Assert.Equal(expected.GetParameters().Select(p => Map(p.ParameterType.FullName!)),
            actual.GetParameters().Select(p => Map(p.ParameterType.FullName!)));
        Assert.Equal(expected.GetParameters().Select(p => p.Name), actual.GetParameters().Select(p => p.Name));
    }

    private static string Map(string name) => name.StartsWith("Avalonia.Controls.", StringComparison.Ordinal)
        ? "UI.Controls." + name["Avalonia.Controls.".Length..]
        : name.StartsWith("Uno.Controls.", StringComparison.Ordinal) ? "UI.Controls." + name["Uno.Controls.".Length..] : name;
}

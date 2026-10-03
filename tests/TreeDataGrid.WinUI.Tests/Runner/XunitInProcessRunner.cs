using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace TreeDataGrid.WinUI.Tests;

/// <summary>
/// Runs xunit facts and theories on the calling (XAML UI) thread, one new instance of the test
/// class per case and disposed afterwards, as xunit does. Covers the features the linked tests
/// use: [Fact], [Theory] with any DataAttribute ([InlineData], [MemberData]), Skip, IDisposable
/// test classes and Task-returning tests.
/// </summary>
internal static class XunitInProcessRunner
{
    public static async Task<int> RunAsync(Assembly assembly, string? filter, TextWriter output)
    {
        int passed = 0, failed = 0, skipped = 0;
        var stopwatch = Stopwatch.StartNew();
        var testClasses = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        foreach (var type in testClasses)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
                .OrderBy(method => method.MetadataToken);
            foreach (var method in methods)
            {
                var fact = method.GetCustomAttribute<FactAttribute>()!;
                foreach (var arguments in Cases(method, fact))
                {
                    var name = $"{type.FullName}.{method.Name}" +
                        (arguments.Length == 0 ? "" : "(" + string.Join(", ", arguments.Select(Format)) + ")");
                    if (filter is not null && !name.Contains(filter, StringComparison.Ordinal)) continue;
                    if (fact.Skip is not null)
                    {
                        ++skipped;
                        continue;
                    }
                    try
                    {
                        await InvokeAsync(type, method, arguments);
                        ++passed;
                    }
                    catch (Exception error)
                    {
                        ++failed;
                        output.WriteLine($"[FAIL] {name}");
                        output.WriteLine(error.ToString());
                    }
                }
            }
        }
        output.WriteLine($"WINUI_TESTS_RESULT: passed={passed}; failed={failed}; skipped={skipped}; total={passed + failed + skipped}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return failed;
    }

    private static IEnumerable<object?[]> Cases(MethodInfo method, FactAttribute fact)
    {
        if (fact is not TheoryAttribute) return [Array.Empty<object?>()];
        // [InlineData(null)] produces a null row: one null argument, as xunit treats it.
        return method.GetCustomAttributes<DataAttribute>().SelectMany(data => data.GetData(method))
            .Select(row => row ?? [null]).ToArray();
    }

    private static async Task InvokeAsync(Type type, MethodInfo method, object?[] data)
    {
        var instance = method.IsStatic ? null : Activator.CreateInstance(type);
        try
        {
            object? result;
            try
            {
                result = method.Invoke(instance, ConvertArguments(method.GetParameters(), data));
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            if (result is Task task) await task;
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    private static object?[] ConvertArguments(ParameterInfo[] parameters, object?[] data)
    {
        var arguments = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; ++i)
        {
            var parameter = parameters[i];
            if (i == parameters.Length - 1 && parameter.IsDefined(typeof(ParamArrayAttribute)) &&
                !(data.Length == parameters.Length && (data[i] is null || parameter.ParameterType.IsInstanceOfType(data[i]))))
            {
                var element = parameter.ParameterType.GetElementType()!;
                var rest = Array.CreateInstance(element, Math.Max(0, data.Length - i));
                for (var j = 0; j < rest.Length; ++j) rest.SetValue(Convert(data[i + j], element), j);
                arguments[i] = rest;
                break;
            }
            arguments[i] = i < data.Length ? Convert(data[i], parameter.ParameterType)
                : parameter.HasDefaultValue ? parameter.DefaultValue
                : throw new InvalidOperationException($"No data for parameter '{parameter.Name}'.");
        }
        return arguments;
    }

    private static object? Convert(object? value, Type type)
    {
        if (value is null || type.IsInstanceOfType(value)) return value;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsEnum) return value is string text ? Enum.Parse(target, text) : Enum.ToObject(target, value);
        if (target == typeof(Guid) && value is string guid) return Guid.Parse(guid);
        if (target == typeof(DateTime) && value is string date) return DateTime.Parse(date, CultureInfo.InvariantCulture);
        if (target == typeof(Type) && value is string typeName) return Type.GetType(typeName, throwOnError: true);
        return value is IConvertible ? System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture) : value;
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        char character => "'" + character + "'",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? value.GetType().Name,
    };
}

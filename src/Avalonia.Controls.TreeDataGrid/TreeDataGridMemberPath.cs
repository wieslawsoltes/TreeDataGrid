using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    internal static class TreeDataGridMemberPath
    {
        private static readonly Func<object, object>[] s_selfLinks = new Func<object, object>[] { x => x };

        public static string[] Parse(string? path)
        {
            return string.IsNullOrWhiteSpace(path) || path == "."
                ? Array.Empty<string>()
                : path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public static IReadOnlyList<MemberInfo>? TryResolve(Type? rootType, string? path)
        {
            if (rootType is null)
                return null;

            var segments = Parse(path);

            if (segments.Length == 0)
                return Array.Empty<MemberInfo>();

            var currentType = rootType;
            var result = new List<MemberInfo>(segments.Length);

            foreach (var segment in segments)
            {
                if (HasUnsupportedSyntax(segment))
                    return null;

                var member = GetMemberInfo(currentType, segment);

                if (member is null)
                    return null;

                result.Add(member);
                currentType = GetMemberType(member);
            }

            return result;
        }

        public static Type? TryGetValueType(Type? rootType, IReadOnlyList<string> segments)
        {
            var current = rootType;

            foreach (var segment in segments)
            {
                if (current is null)
                    return null;

                current = GetMemberType(current, segment);
            }

            return current;
        }

        public static Type? TryGetValueType(IReadOnlyList<MemberInfo>? members, Type? rootType = null)
        {
            return members is { Count: > 0 } ? GetMemberType(members[members.Count - 1]) : rootType;
        }

        public static bool CanWrite(Type? rootType, IReadOnlyList<string> segments)
        {
            if (segments.Count == 0)
                return false;

            if (rootType is null)
                return true;

            var current = rootType;

            for (var i = 0; i < segments.Count - 1; ++i)
            {
                current = GetMemberType(current, segments[i]);

                if (current is null)
                    return true;
            }

            return IsWritableMember(current, segments[segments.Count - 1]);
        }

        public static Func<object, object>[] CreateProgressiveLinks(IReadOnlyList<string> segments)
        {
            if (segments.Count == 0)
                return s_selfLinks;

            var result = new Func<object, object>[segments.Count];

            for (var i = 0; i < segments.Count; ++i)
            {
                var count = i + 1;
                result[i] = model => Read(model, segments, count)!;
            }

            return result;
        }

        public static Func<object, object>[] CreateSubscriptionLinks(IReadOnlyList<MemberInfo> members)
        {
            if (members.Count == 0)
                return s_selfLinks;

            var result = new Func<object, object>[members.Count];
            result[0] = s_selfLinks[0];

            for (var i = 1; i < members.Count; ++i)
            {
                var length = i;
                result[i] = model => Read(model, members, length)!;
            }

            return result;
        }

        public static object? Read(object? model, IReadOnlyList<string> segments, int count)
        {
            object? current = model;

            if (count == 0)
                return current;

            for (var i = 0; i < count; ++i)
            {
                if (current is null)
                    return null;

                current = GetMemberValue(current, segments[i]);
            }

            return current;
        }

        public static object? Read(object? model, IReadOnlyList<MemberInfo> members, int count)
        {
            object? current = model;

            if (count == 0)
                return current;

            for (var i = 0; i < count; ++i)
            {
                if (current is null)
                    return null;

                current = GetMemberValue(members[i], current);
            }

            return current;
        }

        public static object? Write(object model, IReadOnlyList<string> segments, object? value)
        {
            if (segments.Count == 0)
                return null;

            object? current = model;

            for (var i = 0; i < segments.Count - 1; ++i)
            {
                if (current is null)
                    return null;

                current = GetMemberValue(current, segments[i]);
            }

            if (current is null)
                return null;

            return SetMemberValue(current, segments[segments.Count - 1], value);
        }

        public static object? ConvertForSource(object? value, Type? targetType)
        {
            if (value is null || targetType is null)
                return value;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType.IsInstanceOfType(value))
                return value;

            if (underlyingType.IsEnum)
            {
                return value is string s
                    ? Enum.Parse(underlyingType, s, ignoreCase: true)
                    : Enum.ToObject(underlyingType, value);
            }

            if (underlyingType == typeof(string))
                return Convert.ToString(value, CultureInfo.CurrentCulture);

            if (typeof(IEnumerable).IsAssignableFrom(underlyingType) && value is IEnumerable)
                return value;

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlyingType))
                return Convert.ChangeType(value, underlyingType, CultureInfo.CurrentCulture);

            return value;
        }

        public static bool? ConvertToNullableBoolean(object? value)
        {
            return value switch
            {
                null => null,
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                IConvertible => (bool?)Convert.ChangeType(value, typeof(bool), CultureInfo.CurrentCulture),
                _ => null,
            };
        }

        public static object? GetMemberValue(MemberInfo member, object target)
        {
            return member switch
            {
                PropertyInfo property => property.GetValue(target),
                FieldInfo field => field.GetValue(target),
                _ => null,
            };
        }

        public static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => typeof(object),
            };
        }

        private static bool HasUnsupportedSyntax(string segment)
        {
            return segment.Length == 0 ||
                segment.Contains('[') ||
                segment.Contains('(') ||
                segment.Contains('!') ||
                segment.Contains('#') ||
                segment.Contains('$') ||
                segment.Contains('/');
        }

        private static Type? GetMemberType(Type type, string memberName)
        {
            return GetMemberInfo(type, memberName) switch
            {
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => null,
            };
        }

        private static bool IsWritableMember(Type? type, string memberName)
        {
            if (type is null)
                return false;

            return GetMemberInfo(type, memberName) switch
            {
                PropertyInfo property => property.SetMethod is not null,
                FieldInfo field => !field.IsInitOnly,
                _ => false,
            };
        }

        private static MemberInfo? GetMemberInfo(Type type, string memberName)
        {
            return (MemberInfo?)type.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? type.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static object? GetMemberValue(object instance, string memberName)
        {
            return GetMemberInfo(instance.GetType(), memberName) switch
            {
                PropertyInfo property => property.GetValue(instance),
                FieldInfo field => field.GetValue(instance),
                _ => null,
            };
        }

        private static object? SetMemberValue(object instance, string memberName, object? value)
        {
            switch (GetMemberInfo(instance.GetType(), memberName))
            {
                case PropertyInfo property when property.SetMethod is not null:
                    property.SetValue(instance, value);
                    return property.GetValue(instance);
                case FieldInfo field when !field.IsInitOnly:
                    field.SetValue(instance, value);
                    return field.GetValue(instance);
                default:
                    return null;
            }
        }
    }
}

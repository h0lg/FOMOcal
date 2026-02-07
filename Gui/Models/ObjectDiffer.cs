using System.Reflection;
using System.Text.Json.Serialization;

namespace FomoCal;

public sealed class PropertyDiff(List<PropertyInfo> path, object? oldValue, object? newValue)
{
    public List<PropertyInfo> Path { get; } = path;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
    public string DisplayPath => Path.Select(p => p.Name).Join(" ");
}

internal static class ObjectDiffer
{
    internal static IReadOnlyList<PropertyDiff> Diff(object? oldObj, object? newObj, ILookup<Type, string> ignoredProperties)
    {
        var diffs = new List<PropertyDiff>();
        DiffObject(oldObj, newObj, [], diffs, ignoredProperties);
        return diffs;
    }

    private static void DiffObject(object? oldObj, object? newObj, List<PropertyInfo> path,
        List<PropertyDiff> diffs, ILookup<Type, string> ignoredProperties)
    {
        if (oldObj is null && newObj is null) return;
        var type = oldObj?.GetType() ?? newObj!.GetType();
        var ignored = ignoredProperties[type]?.ToArray();

        foreach (var prop in GetSerializableProperties(type, ignored))
        {
            List<PropertyInfo> propPath = [.. path, prop];
            var oldValue = oldObj is null ? null : prop.GetValue(oldObj);
            var newValue = newObj is null ? null : prop.GetValue(newObj);

            if (IsLeaf(prop.PropertyType))
            {
                if (!Equals(oldValue, newValue))
                    diffs.Add(new PropertyDiff(propPath, oldValue, newValue));
            }
            else DiffObject(oldValue, newValue, propPath, diffs, ignoredProperties);
        }
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type, string[]? ignoredNames)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                (ignoredNames?.Contains(p.Name) != true) &&
                p.GetMethod is not null &&
                p.GetIndexParameters().Length == 0 &&
                p.GetCustomAttribute<JsonIgnoreAttribute>() is null);

    private static bool IsLeaf(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;

        return t.IsPrimitive || t.IsEnum || t == typeof(string)
            || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);
    }
}

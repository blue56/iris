namespace Iris.Configuration;

/// <summary>
/// Converts a YAML-deserialized object graph (Dictionary/List/primitives from YamlDotNet)
/// into the colon-delimited flat key/value pairs expected by IConfiguration's in-memory provider.
/// </summary>
internal static class YamlConfigurationFlattener
{
    public static IEnumerable<KeyValuePair<string, string?>> Flatten(object? node, string prefix = "")
    {
        switch (node)
        {
            case Dictionary<object, object> dict:
                foreach (var entry in dict)
                {
                    var key = prefix.Length > 0 ? $"{prefix}:{entry.Key}" : entry.Key.ToString()!;
                    foreach (var pair in Flatten(entry.Value, key))
                        yield return pair;
                }
                break;

            case List<object> list:
                for (var i = 0; i < list.Count; i++)
                {
                    var key = $"{prefix}:{i}";
                    foreach (var pair in Flatten(list[i], key))
                        yield return pair;
                }
                break;

            default:
                yield return new KeyValuePair<string, string?>(prefix, node?.ToString());
                break;
        }
    }
}

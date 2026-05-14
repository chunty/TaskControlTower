using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskTurnstile;

/// <summary>
/// Converts an <see cref="object"/> task key to the string used by the backing store.
/// Strings are used as-is. All other types are serialized to JSON and hashed with SHA-256,
/// prefixed with the type name so keys are identifiable in the database.
/// </summary>
internal static class TaskKeyConverter
{
    internal static string ToKey(object key)
    {
        if (key is string s) return s;
        var type = key.GetType();
        if (type.IsPrimitive || type.IsEnum || key is Guid or decimal or DateTime or DateOnly or TimeOnly or DateTimeOffset)
            return $"{type.FullName ?? type.Name}:{key}";
        var json = JsonSerializer.Serialize(key, type);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"{type.FullName ?? type.Name}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

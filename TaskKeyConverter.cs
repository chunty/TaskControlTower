using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskTurnstile;

/// <summary>
/// Converts an <see cref="object"/> task key to the string stored in the backing store.
/// </summary>
/// <remarks>
/// Conversion rules:
/// <list type="bullet">
///   <item><description><see langword="string"/> — used as-is.</description></item>
///   <item><description>Primitives, enums, <see cref="Guid"/>, <see cref="decimal"/>, <see cref="DateTime"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="DateTimeOffset"/> — <c>{TypeFullName}:{value}</c>, e.g. <c>System.Int32:42</c>.</description></item>
///   <item><description>All other types — JSON-serialised, SHA-256 hashed: <c>{TypeFullName}:{hex}</c>.</description></item>
/// </list>
/// Use this in tests to compute the expected store key when asserting against <see cref="ITaskStateStore"/>.
/// </remarks>
public static class TaskKeyConverter
{
    /// <summary>
    /// Converts <paramref name="key"/> to the string used by the backing store.
    /// </summary>
    public static string ToKey(object key)
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

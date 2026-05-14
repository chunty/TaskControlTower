namespace TaskTurnstile.Tests;

public class TaskKeyConverterTests
{
    // ── string passthrough ────────────────────────────────────────────────────

    [Fact]
    public void ToKey_String_ReturnsAsIs()
    {
        Assert.Equal("import-job", TaskKeyConverter.ToKey("import-job"));
    }

    [Fact]
    public void ToKey_EmptyString_ReturnsEmptyString()
    {
        Assert.Equal("", TaskKeyConverter.ToKey(""));
    }

    // ── primitives and simple value types ────────────────────────────────────

    [Fact]
    public void ToKey_Int_ReturnsPrefixedValue()
    {
        Assert.Equal("System.Int32:42", TaskKeyConverter.ToKey(42));
    }

    [Fact]
    public void ToKey_Long_ReturnsPrefixedValue()
    {
        Assert.Equal("System.Int64:100", TaskKeyConverter.ToKey(100L));
    }

    [Fact]
    public void ToKey_Bool_ReturnsPrefixedValue()
    {
        Assert.Equal("System.Boolean:True", TaskKeyConverter.ToKey(true));
    }

    [Fact]
    public void ToKey_Double_ReturnsPrefixedValue()
    {
        Assert.Equal("System.Double:3.14", TaskKeyConverter.ToKey(3.14));
    }

    [Fact]
    public void ToKey_Decimal_ReturnsPrefixedValue()
    {
        Assert.Equal("System.Decimal:9.99", TaskKeyConverter.ToKey(9.99m));
    }

    [Fact]
    public void ToKey_Guid_ReturnsPrefixedValue()
    {
        var guid = new Guid("12345678-1234-1234-1234-123456789abc");
        Assert.Equal($"System.Guid:{guid}", TaskKeyConverter.ToKey(guid));
    }

    [Fact]
    public void ToKey_DateTime_ReturnsPrefixedValue()
    {
        var dt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var result = TaskKeyConverter.ToKey(dt);
        Assert.StartsWith("System.DateTime:", result);
    }

    [Fact]
    public void ToKey_Enum_ReturnsPrefixedValue()
    {
        Assert.Equal("System.DayOfWeek:Monday", TaskKeyConverter.ToKey(DayOfWeek.Monday));
    }

    // ── complex objects ───────────────────────────────────────────────────────

    private record TestKey(int TenantId, string Type);

    [Fact]
    public void ToKey_ComplexObject_ReturnsPrefixedHash()
    {
        var key = new TestKey(42, "import");
        var result = TaskKeyConverter.ToKey(key);

        Assert.StartsWith("TaskTurnstile.Tests.TaskKeyConverterTests+TestKey:", result);
        // hash is 64 hex chars (SHA-256)
        var hash = result.Split(':')[1];
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ToKey_SameComplexObject_ReturnsSameKey()
    {
        var key1 = new TestKey(42, "import");
        var key2 = new TestKey(42, "import");

        Assert.Equal(TaskKeyConverter.ToKey(key1), TaskKeyConverter.ToKey(key2));
    }

    [Fact]
    public void ToKey_DifferentComplexObjects_ReturnDifferentKeys()
    {
        var key1 = new TestKey(42, "import");
        var key2 = new TestKey(99, "export");

        Assert.NotEqual(TaskKeyConverter.ToKey(key1), TaskKeyConverter.ToKey(key2));
    }
}

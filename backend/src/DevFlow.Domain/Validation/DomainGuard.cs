namespace DevFlow.Domain.Validation;

internal static class DomainGuard
{
    public static string? OptionalText(string? value, int maxLength, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return RequiredText(value, maxLength, parameter);
    }
    public static string RequiredText(string value, int maxLength, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Must be at most {maxLength} characters.", parameter);
        return trimmed;
    }

    public static Guid RequiredId(Guid value, string parameter)
    {
        if (value == Guid.Empty) throw new ArgumentException("An ID is required.", parameter);
        return value;
    }

    public static T DefinedEnum<T>(T value, string parameter) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(parameter);
        return value;
    }
}

namespace MyWindows.Domain.Profiles;

public static class IdentifierRules
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.Skip(1).All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }
}

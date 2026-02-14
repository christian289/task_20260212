namespace CompanyC.Api.Parsers;

internal static class DateParsingHelper
{
    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd"];

    internal static bool TryParseDate(string? value, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        return DateTime.TryParseExact(value.Trim(), Formats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}

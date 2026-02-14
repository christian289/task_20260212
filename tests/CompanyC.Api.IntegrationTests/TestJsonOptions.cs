namespace CompanyC.Api.IntegrationTests;

internal static class TestJsonOptions
{
    internal static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}

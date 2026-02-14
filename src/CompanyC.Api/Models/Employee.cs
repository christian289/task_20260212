namespace CompanyC.Api.Models;

public sealed class Employee
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Tel { get; init; }
    public DateTime Joined { get; init; }
    public Dictionary<string, string> ExtraFields { get; init; } = [];
}

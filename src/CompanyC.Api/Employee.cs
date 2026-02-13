namespace CompanyC.Api;

public sealed class Employee
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public DateTime Joined { get; set; }
    public Dictionary<string, string> ExtraFields { get; set; } = [];
}

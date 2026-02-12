namespace CompanyC.Api;

public sealed record Employee
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public DateTime JoinedDate { get; init; }
}

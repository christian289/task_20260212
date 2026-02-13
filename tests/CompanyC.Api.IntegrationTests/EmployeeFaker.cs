namespace CompanyC.Api.IntegrationTests;

public static class EmployeeFaker
{
    private static readonly string[] Surnames = ["김", "이", "박", "최", "정", "강", "조", "윤", "장", "임"];
    private static readonly string[] GivenChars = ["민", "서", "지", "수", "현", "준", "우", "하", "은", "도", "영", "재", "호", "진", "성", "경", "태", "혁"];

    private static readonly Faker<Employee> Faker = new Faker<Employee>()
        .CustomInstantiator(f => new Employee
        {
            Name = f.PickRandom(Surnames) + f.PickRandom(GivenChars) + f.PickRandom(GivenChars),
            Email = f.Internet.Email(),
            Phone = "010" + f.Random.Number(10000000, 99999999).ToString(),
            JoinedDate = f.Date.Between(new DateTime(2010, 1, 1), new DateTime(2024, 12, 31))
        });

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static List<Employee> Generate(int count) => Faker.Generate(count);

    public static string GenerateCsv(int count)
    {
        var employees = Generate(count);
        var sb = new StringBuilder();
        foreach (var e in employees)
        {
            sb.AppendLine($"{e.Name}, {e.Email} {e.Phone}, {e.JoinedDate:yyyy.MM.dd}");
        }
        return sb.ToString();
    }

    public static string GenerateJson(int count)
    {
        var employees = Generate(count);
        var data = employees.Select(e => new
        {
            name = e.Name,
            email = e.Email,
            tel = e.Phone,
            joined = e.JoinedDate.ToString("yyyy-MM-dd")
        }).ToList();
        return JsonSerializer.Serialize(data, JsonWriteOptions);
    }
}

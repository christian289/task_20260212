using Bogus;
using System.Text;
using System.Text.Json;

var count = 10;
var format = "both";
var outputPath = Directory.GetCurrentDirectory();

// Parse command line arguments
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--count" && i + 1 < args.Length)
    {
        count = int.Parse(args[i + 1]);
        i++;
    }
    else if (args[i] == "--format" && i + 1 < args.Length)
    {
        format = args[i + 1].ToLower();
        i++;
    }
    else if (args[i] == "--output" && i + 1 < args.Length)
    {
        outputPath = args[i + 1];
        i++;
    }
}

// Korean surnames and given name characters
var surnames = new[] { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임" };
var givenNameChars = new[] { "민", "서", "지", "수", "현", "준", "우", "하", "은", "도", "윤", "아", "영", "재", "호", "진", "성", "경", "태", "혁" };

// Romanization map for email generation
var romanization = new Dictionary<string, string>
{
    { "김", "kim" }, { "이", "lee" }, { "박", "park" }, { "최", "choi" }, { "정", "jung" },
    { "강", "kang" }, { "조", "cho" }, { "윤", "yoon" }, { "장", "jang" }, { "임", "lim" },
    { "민", "min" }, { "서", "seo" }, { "지", "ji" }, { "수", "su" }, { "현", "hyun" },
    { "준", "jun" }, { "우", "woo" }, { "하", "ha" }, { "은", "eun" }, { "도", "do" },
    { "아", "ah" }, { "영", "young" }, { "재", "jae" }, { "호", "ho" },
    { "진", "jin" }, { "성", "sung" }, { "경", "kyung" }, { "태", "tae" }, { "혁", "hyuk" }
};

// Generate employees using Bogus
var faker = new Faker();
var employees = new List<Employee>();

for (int i = 0; i < count; i++)
{
    var surname = faker.PickRandom(surnames);
    var givenName = faker.PickRandom(givenNameChars) + faker.PickRandom(givenNameChars);
    var name = surname + givenName;

    // Generate romanized name for email
    var romanizedSurname = romanization[surname];
    var romanizedGivenName = "";
    foreach (var ch in givenName)
    {
        if (romanization.ContainsKey(ch.ToString()))
        {
            romanizedGivenName += romanization[ch.ToString()];
        }
    }
    var romanizedName = romanizedGivenName + romanizedSurname;

    var email = faker.Internet.Email(romanizedName);
    var phone = "010" + faker.Random.Number(10000000, 99999999).ToString();
    var joinedDate = faker.Date.Between(new DateTime(2010, 1, 1), new DateTime(2024, 12, 31));

    employees.Add(new Employee
    {
        Name = name,
        Email = email,
        Tel = phone,
        Joined = joinedDate
    });
}

// Ensure output directory exists
Directory.CreateDirectory(outputPath);

// Generate CSV
if (format is "csv" or "both")
{
    var csvPath = Path.Combine(outputPath, "employees.csv");
    var csvContent = new StringBuilder();

    foreach (var emp in employees)
    {
        csvContent.AppendLine($"{emp.Name}, {emp.Email} {emp.Tel}, {emp.Joined:yyyy.MM.dd}");
    }

    File.WriteAllText(csvPath, csvContent.ToString(), Encoding.UTF8);
    Console.WriteLine($"Generated {count} employees -> {csvPath}");
}

// Generate JSON
if (format is "json" or "both")
{
    var jsonPath = Path.Combine(outputPath, "employees.json");
    var jsonData = employees.Select(e => new
    {
        name = e.Name,
        email = e.Email,
        tel = e.Tel,
        joined = e.Joined.ToString("yyyy-MM-dd")
    }).ToList();

    var jsonContent = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);
    Console.WriteLine($"Generated {count} employees -> {jsonPath}");
}

Console.WriteLine($"Data generation complete! Count: {count}, Format: {format}");

record Employee
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Tel { get; init; }
    public required DateTime Joined { get; init; }
}

namespace CompanyC.Api;

public sealed class SqliteEmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SqliteEmployeeRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = QueryLoader.Get("EnableWalMode");
        pragmaCmd.ExecuteNonQuery();

        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = QueryLoader.Get("CreateTable");
        createCmd.ExecuteNonQuery();
    }

    public (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = QueryLoader.Get("Count");
        var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

        using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = QueryLoader.Get("SelectPaged");
        selectCmd.Parameters.AddWithValue("@limit", pageSize);
        selectCmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var items = new List<Employee>();
        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadEmployee(reader));
        }

        return (items, totalCount);
    }

    public Employee? GetByName(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = QueryLoader.Get("SelectByName");
        command.Parameters.AddWithValue("@name", name);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEmployee(reader) : null;
    }

    public void AddRange(List<Employee> employees)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.CommandText = QueryLoader.Get("Insert");

        var nameParam = command.Parameters.Add("@name", SqliteType.Text);
        var emailParam = command.Parameters.Add("@email", SqliteType.Text);
        var phoneParam = command.Parameters.Add("@phone", SqliteType.Text);
        var joinedDateParam = command.Parameters.Add("@joinedDate", SqliteType.Text);
        var extraFieldsParam = command.Parameters.Add("@extraFields", SqliteType.Text);

        foreach (var employee in employees)
        {
            nameParam.Value = employee.Name;
            emailParam.Value = employee.Email;
            phoneParam.Value = employee.Phone;
            joinedDateParam.Value = employee.JoinedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            extraFieldsParam.Value = employee.ExtraFields.Count > 0
                ? JsonSerializer.Serialize(employee.ExtraFields, JsonOptions)
                : (object)DBNull.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static Employee ReadEmployee(SqliteDataReader reader)
    {
        var extraJson = reader.IsDBNull(4) ? null : reader.GetString(4);
        var extraFields = string.IsNullOrEmpty(extraJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(extraJson, JsonOptions) ?? [];

        return new Employee
        {
            Name = reader.GetString(0),
            Email = reader.GetString(1),
            Phone = reader.GetString(2),
            JoinedDate = DateTime.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ExtraFields = extraFields
        };
    }
}

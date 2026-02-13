namespace CompanyC.Api;

public sealed partial class SqliteEmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;
    private static readonly HashSet<string> BaseColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Name", "Email", "Tel", "Joined"
    };

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z_\d]*$")]
    private static partial Regex SafeColumnNamePattern();

    public SqliteEmployeeRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    private static bool IsValidColumnName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.Length <= 128
            && !name.Any(char.IsControl)
            && !BaseColumns.Contains(name)
            && SafeColumnNamePattern().IsMatch(name);
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
        if (employees.Count == 0)
            return;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // 새로운 ExtraFields 컬럼이 필요하면 동적으로 추가
        var extraKeys = employees
            .SelectMany(e => e.ExtraFields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsValidColumnName)
            .ToList();

        if (extraKeys.Count > 0)
            EnsureColumns(connection, extraKeys);

        // 동적 INSERT 구성
        var allColumns = new List<string> { "Name", "Email", "Tel", "Joined" };
        allColumns.AddRange(extraKeys);

        var quotedColumns = allColumns.Select(c => $"\"{c}\"");
        var paramNames = allColumns.Select((_, i) => $"@p{i}").ToList();
        var insertSql = $"INSERT INTO Employees ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", paramNames)})";

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = insertSql;

        var parameters = new SqliteParameter[allColumns.Count];
        for (var i = 0; i < allColumns.Count; i++)
        {
            parameters[i] = command.Parameters.Add($"@p{i}", SqliteType.Text);
        }

        foreach (var employee in employees)
        {
            parameters[0].Value = employee.Name;
            parameters[1].Value = employee.Email;
            parameters[2].Value = employee.Tel;
            parameters[3].Value = employee.Joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            for (var i = 4; i < allColumns.Count; i++)
            {
                var key = allColumns[i];
                parameters[i].Value = employee.ExtraFields.TryGetValue(key, out var val)
                    ? val
                    : (object)DBNull.Value;
            }

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void EnsureColumns(SqliteConnection connection, List<string> requiredColumns)
    {
        var existingColumns = GetExistingColumns(connection);

        foreach (var col in requiredColumns)
        {
            if (existingColumns.Contains(col))
                continue;

            if (!IsValidColumnName(col))
                continue;

            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = string.Format(QueryLoader.Get("AddColumn"), $"\"{col}\"");
            alterCmd.ExecuteNonQuery();
            existingColumns.Add(col);
        }
    }

    private HashSet<string> GetExistingColumns(SqliteConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = QueryLoader.Get("TableInfo");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1)); // column name is at index 1
        }

        return columns;
    }

    private static Employee ReadEmployee(SqliteDataReader reader)
    {
        var extraFields = new Dictionary<string, string>();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (BaseColumns.Contains(columnName) || reader.IsDBNull(i))
                continue;

            extraFields[columnName] = reader.GetString(i);
        }

        return new Employee
        {
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            Tel = reader.GetString(reader.GetOrdinal("Tel")),
            Joined = DateTime.ParseExact(
                reader.GetString(reader.GetOrdinal("Joined")),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            ExtraFields = extraFields
        };
    }
}

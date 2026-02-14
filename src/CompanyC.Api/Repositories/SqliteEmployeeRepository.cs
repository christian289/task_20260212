using CompanyC.Api.Errors;
using CompanyC.Api.Models;

namespace CompanyC.Api.Repositories;

public sealed partial class SqliteEmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteEmployeeRepository> _logger;
    private static readonly HashSet<string> BaseColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hash", "Name", "Email", "Tel", "Joined"
    };

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z_\d]*$")]
    private static partial Regex SafeColumnNamePattern();

    public SqliteEmployeeRepository(string connectionString, ILogger<SqliteEmployeeRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
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

    internal static string ComputeHash(Employee employee)
    {
        var input = $"{employee.Name}|{employee.Email}|{employee.Tel}|{employee.Joined:yyyy-MM-dd}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hashBytes);
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

        _logger.DatabaseInitialized();
    }

    public (IReadOnlyList<Employee> Items, int TotalCount) GetAll(int page, int pageSize)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // COUNT와 SELECT를 동일 트랜잭션으로 묶어 동시 INSERT에 의한 불일치 방지
        using var transaction = connection.BeginTransaction();

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

        transaction.Commit();
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

    public ErrorOr<List<Employee>> AddRange(List<Employee> employees)
    {
        if (employees.Count == 0)
            return new List<Employee>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 새로운 ExtraFields 컬럼이 필요하면 동적으로 추가
            var extraKeys = employees
                .SelectMany(e => e.ExtraFields.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(IsValidColumnName)
                .ToList();

            // DDL과 INSERT를 동일 트랜잭션으로 묶어 동시 AddRange 호출 간 경합 방지
            using var transaction = connection.BeginTransaction();

            if (extraKeys.Count > 0)
                EnsureColumns(connection, extraKeys);

            // 동적 INSERT 구성 (Hash PK 기반 중복 무시)
            var allColumns = new List<string> { "Hash", "Name", "Email", "Tel", "Joined" };
            allColumns.AddRange(extraKeys);

            var quotedColumns = allColumns.Select(c => $"\"{c}\"");
            var paramNames = allColumns.Select((_, i) => $"@p{i}").ToList();
            var insertSql = $"INSERT OR IGNORE INTO Employees ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", paramNames)})";
            using var command = connection.CreateCommand();
            command.CommandText = insertSql;

            var parameters = new SqliteParameter[allColumns.Count];
            for (var i = 0; i < allColumns.Count; i++)
            {
                parameters[i] = command.Parameters.Add($"@p{i}", SqliteType.Text);
            }

            var inserted = new List<Employee>();
            foreach (var employee in employees)
            {
                parameters[0].Value = ComputeHash(employee);
                parameters[1].Value = employee.Name;
                parameters[2].Value = employee.Email;
                parameters[3].Value = employee.Tel;
                parameters[4].Value = employee.Joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                for (var i = 5; i < allColumns.Count; i++)
                {
                    var key = allColumns[i];
                    parameters[i].Value = employee.ExtraFields.TryGetValue(key, out var val)
                        ? val
                        : (object)DBNull.Value;
                }

                if (command.ExecuteNonQuery() > 0)
                    inserted.Add(employee);
            }

            var skippedCount = employees.Count - inserted.Count;
            if (skippedCount > 0)
                _logger.DuplicateEmployeesSkipped(skippedCount);

            _logger.InsertCompleted(inserted.Count);
            transaction.Commit();
            return inserted;
        }
        catch (Exception ex)
        {
            _logger.StorageError(ex);
            return EmployeeErrors.StorageFailed;
        }
    }

    public ErrorOr<Employee> Update(string currentHash, Employee updated)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var newHash = ComputeHash(updated);

            // 변경된 해시가 이미 존재하는지 확인 (다른 직원과 중복 방지)
            if (newHash != currentHash)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = QueryLoader.Get("SelectByHash");
                checkCmd.Parameters.AddWithValue("@hash", newHash);
                using var checkReader = checkCmd.ExecuteReader();
                if (checkReader.Read())
                {
                    return EmployeeErrors.DuplicateAfterUpdate;
                }
            }

            using var command = connection.CreateCommand();
            command.CommandText = QueryLoader.Get("UpdateByHash");
            command.Parameters.AddWithValue("@newHash", newHash);
            command.Parameters.AddWithValue("@name", updated.Name);
            command.Parameters.AddWithValue("@email", updated.Email);
            command.Parameters.AddWithValue("@tel", updated.Tel);
            command.Parameters.AddWithValue("@joined", updated.Joined.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@oldHash", currentHash);

            var affected = command.ExecuteNonQuery();
            if (affected == 0)
            {
                _logger.EmployeeNotFoundByHash(currentHash);
                return EmployeeErrors.NotFound(updated.Name);
            }

            _logger.EmployeeUpdated(updated.Name);
            return updated;
        }
        catch (Exception ex)
        {
            _logger.StorageError(ex);
            return EmployeeErrors.StorageFailed;
        }
    }

    private void EnsureColumns(SqliteConnection connection, List<string> requiredColumns)
    {
        var existingColumns = GetExistingColumns(connection);

        foreach (var col in requiredColumns)
        {
            if (existingColumns.Contains(col))
                continue;

            if (!IsValidColumnName(col))
            {
                _logger.InvalidColumnNameIgnored(col);
                continue;
            }

            try
            {
                // DDL은 파라미터화 불가 — IsValidColumnName 검증에 의존 (방어적 assertion)
                System.Diagnostics.Debug.Assert(SafeColumnNamePattern().IsMatch(col), $"Invalid column name bypassed validation: {col}");
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = string.Format(QueryLoader.Get("AddColumn"), $"\"{col}\"");
                alterCmd.ExecuteNonQuery();
                _logger.DynamicColumnAdded(col);
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                _logger.ColumnAlreadyExists(col);
            }

            existingColumns.Add(col);
        }
    }

    private static HashSet<string> GetExistingColumns(SqliteConnection connection)
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
            Joined = DateTime.TryParseExact(
                reader.GetString(reader.GetOrdinal("Joined")),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var joinedDate) ? joinedDate : default,
            ExtraFields = extraFields
        };
    }
}

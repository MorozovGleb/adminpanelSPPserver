using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using SPP.Serever.Models;
using Microsoft.Extensions.Configuration;

public class ScheduleRepository
{
    private readonly string _connectionString;

    public ScheduleRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<List<WorkerModel>> GetWorkersAsync()
    {
        var workers = new List<WorkerModel>();

        using SqlConnection connection = new SqlConnection(_connectionString);

        await connection.OpenAsync();

        string sql = @"
       SELECT 
    u.ID AS WorkerId,
    u.Name AS WorkerName,
    v.Name AS Position,
    c._Name AS Code
FROM Users u
INNER JOIN Confirmation_verification cv ON cv.ID_User = u.ID
INNER JOIN Verification v ON v.ID = cv.ID_Verification
INNER JOIN Code c ON c.ID = v.Code
WHERE v.Name != 'Вводное ознакомление с инструктором'
ORDER BY u.ID ASC";

        using SqlCommand command = new SqlCommand(sql, connection);

        using SqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            workers.Add(new WorkerModel
            {
                WorkerId = reader.GetInt32(reader.GetOrdinal("WorkerId")),
                WorkerName = reader.GetString(reader.GetOrdinal("WorkerName")),
                Position = reader.GetString(reader.GetOrdinal("Position")),
                Code = reader.GetString(reader.GetOrdinal("Code"))
            });
        }

        return workers;
    }
}
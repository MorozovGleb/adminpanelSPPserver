using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SPP.Serever.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Добавляем DbContext
builder.Services.AddDbContext<SppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        }));

// Проверка подключения
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine("=== CONNECTION STRING ===");
Console.WriteLine(connectionString);
Console.WriteLine("=========================");

using (var conn = new SqlConnection(connectionString))
{
    try
    {
        conn.Open();
        Console.WriteLine("SQL CONNECTION OPENED SUCCESSFULLY");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"SQL CONNECTION ERROR: {ex.Message}");
    }
}

// 2. Добавляем контроллеры (ТОЛЬКО ОДИН РАЗ!)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// 3. Добавляем Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Регистрируем сервисы
builder.Services.AddHttpClient();
builder.Services.AddScoped<ScheduleGenerator>();
// builder.Services.AddScoped<ScheduleRepository>(); // Раскомментируй если есть

var app = builder.Build();

// 5. Настраиваем middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SPP API V1");
    c.RoutePrefix = "swagger"; // Swagger будет доступен по /swagger
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 6. Выводим информацию о запуске
Console.WriteLine("================================================");
Console.WriteLine("Сервер запущен!");
Console.WriteLine("Swagger: https://localhost:7048/swagger");
Console.WriteLine("================================================");

app.Run();
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPP.Serever.Models;

namespace SPP.Serever.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly SppDbContext _db;

        public ScheduleController(SppDbContext db)
        {
            _db = db;
        }

        // ТЕСТОВЫЙ ПИНГ
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { message = "pong", time = DateTime.Now });
        }

        // СТАТУС СИСТЕМЫ
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            try
            {
                var userCount = await _db.Users.CountAsync();
                var scheduleCount = await _db.Schedules.CountAsync();
                var preferencesCount = await _db.Set<WordTimeReady>().CountAsync();

                return Ok(new
                {
                    status = "API работает",
                    timestamp = DateTime.Now,
                    counts = new
                    {
                        users = userCount,
                        schedules = scheduleCount,
                        workTimePreferences = preferencesCount
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ПОЛУЧИТЬ МАКСИМАЛЬНЫЙ ID
        private async Task<int> GetNextScheduleId()
        {
            try
            {
                var maxId = await _db.Schedules.MaxAsync(s => (int?)s.ID) ?? 0;
                return maxId + 1;
            }
            catch
            {
                return 1;
            }
        }

        // Парсинг времени из строки типа "08:00-17:00"
        private (TimeSpan start, TimeSpan end) ParseTimePreference(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
                return (new TimeSpan(0, 0, 0), new TimeSpan(0, 0, 0)); // Возвращаем нули чтобы пропустить

            var parts = timeString.Split('-');
            if (parts.Length == 2)
            {
                if (TimeSpan.TryParse(parts[0].Trim(), out var start) &&
                    TimeSpan.TryParse(parts[1].Trim(), out var end))
                {
                    return (start, end);
                }
            }

            return (new TimeSpan(0, 0, 0), new TimeSpan(0, 0, 0));
        }

        // Получить предпочтения времени для пользователя на конкретный день недели
        private (TimeSpan start, TimeSpan end) GetUserTimePreference(WordTimeReady preference, DayOfWeek dayOfWeek)
        {
            string timeString = dayOfWeek switch
            {
                DayOfWeek.Monday => preference?.Monday,
                DayOfWeek.Tuesday => preference?.Tuesday,
                DayOfWeek.Wednesday => preference?.Wednesday,
                DayOfWeek.Thursday => preference?.Thursday,
                DayOfWeek.Friday => preference?.Friday,
                DayOfWeek.Saturday => preference?.Saturday,
                DayOfWeek.Sunday => preference?.Sunday,
                _ => null
            };

            return ParseTimePreference(timeString);
        }

        // ГЕНЕРАЦИЯ РАСПИСАНИЯ
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GenerateRequest request)
        {
            try
            {
                Console.WriteLine($"========================================");
                Console.WriteLine($"===== ГЕНЕРАЦИЯ РАСПИСАНИЯ =====");
                Console.WriteLine($"Период: {request.StartDate:yyyy-MM-dd} - {request.StartDate.AddDays(request.DaysCount - 1):yyyy-MM-dd}");
                Console.WriteLine($"========================================");

                // 0. Очищаем всё расписание
                Console.WriteLine("\n🗑️ Очистка старого расписания...");
                var deleteSql = "DELETE FROM [Schedule]";
                var deletedCount = await _db.Database.ExecuteSqlRawAsync(deleteSql);
                Console.WriteLine($"Удалено записей: {deletedCount}");

                // 1. Загружаем данные
                Console.WriteLine("\n📥 Загрузка данных...");
                var users = await _db.Users.ToListAsync();
                var daysList = await _db.Days.ToListAsync();
                var workTimePreferences = await _db.Set<WordTimeReady>()
                    .FromSqlRaw("SELECT * FROM [WordTimeReady]")
                    .ToListAsync();

                Console.WriteLine($"Пользователей: {users.Count}");
                Console.WriteLine($"Дней в справочнике: {daysList.Count}");

                // Выводим содержимое Days для отладки
                Console.WriteLine("Содержимое таблицы Days:");
                foreach (var day in daysList)
                {
                    Console.WriteLine($"  ID={day.ID}, Name='{day.Name}'");
                }

                if (!users.Any())
                    return BadRequest(new { error = "Нет пользователей" });
                if (!daysList.Any())
                    return BadRequest(new { error = "Нет данных в таблице Days" });

                // 2. Создаем маппинг дней
                // Создаем словарь: номер дня недели (1=Пн ... 7=Вс) -> ID из таблицы Days
                var dayMapping = new Dictionary<int, int>();

                // Пробуем найти соответствия по именам
                var dayNames = new Dictionary<int, string[]>
        {
            { 1, new[] { "Monday", "Понедельник", "Пн" } },
            { 2, new[] { "Tuesday", "Вторник", "Вт" } },
            { 3, new[] { "Wednesday", "Среда", "Ср" } },
            { 4, new[] { "Thursday", "Четверг", "Чт" } },
            { 5, new[] { "Friday", "Пятница", "Пт" } },
            { 6, new[] { "Saturday", "Суббота", "Сб" } },
            { 7, new[] { "Sunday", "Воскресенье", "Вс" } }
        };

                foreach (var kvp in dayNames)
                {
                    int dayNumber = kvp.Key;
                    string[] possibleNames = kvp.Value;

                    // Ищем день в таблице Days по любому из возможных имен
                    var foundDay = daysList.FirstOrDefault(d =>
                        possibleNames.Any(name => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

                    if (foundDay != null)
                    {
                        dayMapping[dayNumber] = foundDay.ID;
                        Console.WriteLine($"  День {dayNumber} ({possibleNames[0]}) -> ID={foundDay.ID}");
                    }
                }

                // Если не нашли все 7 дней, используем прямое соответствие ID
                if (dayMapping.Count < 7)
                {
                    Console.WriteLine("  Использую прямое соответствие ID = номеру дня");
                    for (int i = 1; i <= 7; i++)
                    {
                        if (!dayMapping.ContainsKey(i))
                        {
                            var day = daysList.FirstOrDefault(d => d.ID == i);
                            if (day != null)
                            {
                                dayMapping[i] = day.ID;
                                Console.WriteLine($"  День {i} -> ID={day.ID} (по номеру)");
                            }
                        }
                    }
                }

                // Если всё ещё нет - берём первый доступный ID для всех
                int fallbackDayId = daysList.First().ID;
                Console.WriteLine($"  Резервный ID дня: {fallbackDayId}");

                // 3. Пользователи с настройками времени
                var preferencesByUser = workTimePreferences
                    .Where(p => p.ID_User.HasValue && p.ID_User.Value > 0)
                    .ToDictionary(p => p.ID_User.Value);

                Console.WriteLine($"\nПользователей с настройками времени: {preferencesByUser.Count}");

                // 4. Получаем начальный ID
                var nextId = await GetNextScheduleId();

                // 5. Генерируем смены
                Console.WriteLine("\n📋 Генерация смен...");
                var createdSchedules = new List<object>();
                int totalCreated = 0;

                for (int dayIndex = 0; dayIndex < request.DaysCount; dayIndex++)
                {
                    var currentDate = request.StartDate.AddDays(dayIndex);
                    var dayOfWeek = currentDate.DayOfWeek;

                    // Конвертируем DayOfWeek в номер (1=Пн ... 7=Вс)
                    int dayNumber = dayOfWeek switch
                    {
                        DayOfWeek.Monday => 1,
                        DayOfWeek.Tuesday => 2,
                        DayOfWeek.Wednesday => 3,
                        DayOfWeek.Thursday => 4,
                        DayOfWeek.Friday => 5,
                        DayOfWeek.Saturday => 6,
                        DayOfWeek.Sunday => 7,
                        _ => 1
                    };

                    // Получаем правильный ID дня
                    int dayId = dayMapping.ContainsKey(dayNumber) ? dayMapping[dayNumber] : fallbackDayId;

                    Console.WriteLine($"\n📅 День {dayIndex + 1}: {currentDate:yyyy-MM-dd} ({dayOfWeek}) -> ID_Day={dayId}");

                    // Берём пользователей с настройками времени
                    var usersWithPreferences = users.Where(u => preferencesByUser.ContainsKey(u.ID)).ToList();

                    if (!usersWithPreferences.Any())
                    {
                        Console.WriteLine("  ⚠️ Нет пользователей с настройками времени");
                        continue;
                    }

                    // Для каждого пользователя создаём смену
                    foreach (var user in usersWithPreferences)
                    {
                        try
                        {
                            var preference = preferencesByUser[user.ID];
                            var (startTime, endTime) = GetUserTimePreference(preference, dayOfWeek);

                            // Пропускаем если время не указано
                            if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
                            {
                                continue;
                            }

                            var sql = @"INSERT INTO [Schedule] ([ID], [ID_User], [ID_Verification], [_Date], [_Start], [_End], [ID_Day]) 
                                VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";

                            await _db.Database.ExecuteSqlRawAsync(sql,
                                nextId,
                                user.ID,
                                1,
                                currentDate.Date,
                                startTime,
                                endTime,
                                dayId);

                            createdSchedules.Add(new
                            {
                                id = nextId,
                                userId = user.ID,
                                userName = $"{user.Surname} {user.Name}",
                                date = currentDate.ToString("yyyy-MM-dd"),
                                dayOfWeek = dayOfWeek.ToString(),
                                dayId = dayId,
                                start = startTime.ToString(@"hh\:mm"),
                                end = endTime.ToString(@"hh\:mm")
                            });

                            totalCreated++;
                            nextId++;
                        }
                        catch (Exception insertEx)
                        {
                            Console.WriteLine($"  ✗ Ошибка для User {user.ID}: {insertEx.Message}");
                        }
                    }
                }

                Console.WriteLine($"\n========================================");
                Console.WriteLine($"✅ ВСЕГО СОЗДАНО: {totalCreated} смен");
                Console.WriteLine($"========================================");

                return Ok(new
                {
                    message = $"Расписание успешно сгенерировано!",
                    deleted = deletedCount,
                    created = totalCreated,
                    startDate = request.StartDate,
                    endDate = request.StartDate.AddDays(request.DaysCount - 1),
                    daysCount = request.DaysCount,
                    usersWithPreferences = preferencesByUser.Count,
                    schedules = createdSchedules
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА: {ex.Message}");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // ПРОВЕРКА СОДЕРЖИМОГО WordTimeReady
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            try
            {
                var preferences = await _db.Set<WordTimeReady>()
                    .FromSqlRaw("SELECT * FROM [WordTimeReady]")
                    .Where(p => p.ID_User.HasValue && p.ID_User.Value > 0)
                    .Take(10)
                    .ToListAsync();

                var result = preferences.Select(p => new
                {
                    p.ID,
                    userId = p.ID_User,
                    monday = p.Monday,
                    tuesday = p.Tuesday,
                    wednesday = p.Wednesday,
                    thursday = p.Thursday,
                    friday = p.Friday,
                    saturday = p.Saturday,
                    sunday = p.Sunday
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ПОЛУЧИТЬ СУЩЕСТВУЮЩЕЕ РАСПИСАНИЕ
        [HttpGet("existing")]
        public async Task<IActionResult> GetExisting([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var query = _db.Schedules.AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(s => s._Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(s => s._Date <= toDate.Value);

                var schedules = await query
                    .OrderBy(s => s.ID_User)  // Сначала сортируем по пользователю
                    .ThenBy(s => s._Date)     // Потом по дате
                    .ThenBy(s => s._Start)    // Потом по времени
                    .ToListAsync();

                // Получаем всех пользователей
                var userIds = schedules.Select(s => s.ID_User).Distinct();
                var users = await _db.Users.Where(u => userIds.Contains(u.ID)).ToDictionaryAsync(u => u.ID);
                var days = await _db.Days.ToDictionaryAsync(d => d.ID);

                // Группируем по пользователям
                var groupedByUser = schedules
                    .GroupBy(s => s.ID_User)
                    .Select(g =>
                    {
                        var userId = g.Key;
                        var userName = users.ContainsKey(userId)
                            ? $"{users[userId].Surname} {users[userId].Name}"
                            : "Unknown";

                        var shifts = g.Select(s => new
                        {
                            id = s.ID,
                            date = s._Date.ToString("yyyy-MM-dd"),
                            dayOfWeek = days.ContainsKey(s.ID_Day) ? days[s.ID_Day].Name : s._Date.DayOfWeek.ToString(),
                            start = s._Start.ToString(@"hh\:mm"),
                            end = s._End.ToString(@"hh\:mm")
                        }).ToList();

                        return new
                        {
                            userId = userId,
                            userName = userName,
                            totalShifts = shifts.Count,
                            shifts = shifts
                        };
                    })
                    .ToList();

                return Ok(new
                {
                    totalUsers = groupedByUser.Count,
                    totalShifts = schedules.Count,
                    users = groupedByUser
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ОЧИСТИТЬ РАСПИСАНИЕ
        [HttpDelete("clear")]
        public async Task<IActionResult> Clear([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                int deleted;
                if (fromDate.HasValue && toDate.HasValue)
                {
                    var sql = "DELETE FROM [Schedule] WHERE [_Date] >= @p0 AND [_Date] <= @p1";
                    deleted = await _db.Database.ExecuteSqlRawAsync(sql, fromDate.Value, toDate.Value);
                }
                else
                {
                    var sql = "DELETE FROM [Schedule]";
                    deleted = await _db.Database.ExecuteSqlRawAsync(sql);
                }

                return Ok(new { message = $"Удалено {deleted} записей" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        // Вспомогательный приватный метод для получения расписания пользователя
        private async Task<IActionResult> GetUserSchedule(int userId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _db.Schedules.Where(s => s.ID_User == userId);

                if (fromDate.HasValue)
                    query = query.Where(s => s._Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(s => s._Date <= toDate.Value);

                var schedules = await query
                    .OrderBy(s => s._Date)
                    .ThenBy(s => s._Start)
                    .ToListAsync();

                var user = await _db.Users.FirstOrDefaultAsync(u => u.ID == userId);

                if (user == null)
                    return NotFound(new { error = $"Пользователь с ID {userId} не найден" });

                var days = await _db.Days.ToDictionaryAsync(d => d.ID);

                var shifts = schedules.Select(s => new
                {
                    id = s.ID,
                    date = s._Date.ToString("yyyy-MM-dd"),
                    dayOfWeek = days.ContainsKey(s.ID_Day) ? days[s.ID_Day].Name : s._Date.DayOfWeek.ToString(),
                    start = s._Start.ToString(@"hh\:mm"),
                    end = s._End.ToString(@"hh\:mm")
                }).ToList();

                return Ok(new
                {
                    userId = user.ID,
                    userName = $"{user.Surname} {user.Name}",
                    role = user.ID_Role,
                    totalShifts = shifts.Count,
                    shifts = shifts
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ПОЛУЧИТЬ РАСПИСАНИЕ КОНКРЕТНОГО ПОЛЬЗОВАТЕЛЯ ПО ID (для админа)
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserScheduleById(int userId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            return await GetUserSchedule(userId, fromDate, toDate);
        }

        // ПОЛУЧИТЬ РАСПИСАНИЕ ТЕКУЩЕГО АВТОРИЗОВАННОГО ПОЛЬЗОВАТЕЛЯ
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetMySchedule([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Пользователь не авторизован" });

                return await GetUserSchedule(userId.Value, fromDate, toDate);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Вспомогательный метод для получения ID текущего пользователя
        private int? GetCurrentUserId()
        {
            // Способ 1: Из JWT claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "sub" || c.Type == "id");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int claimUserId))
            {
                Console.WriteLine($"User ID из claims: {claimUserId}");
                return claimUserId;
            }

            // Способ 2: Из заголовка X-User-Id
            var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(userIdHeader) && int.TryParse(userIdHeader, out int headerUserId))
            {
                Console.WriteLine($"User ID из заголовка: {headerUserId}");
                return headerUserId;
            }

            // Способ 3: Из куки UserId
            if (Request.Cookies.TryGetValue("UserId", out string cookieUserId) && int.TryParse(cookieUserId, out int cookieId))
            {
                Console.WriteLine($"User ID из куки: {cookieId}");
                return cookieId;
            }

            Console.WriteLine("User ID не найден");
            return null;
        }

        // ОТЛАДОЧНАЯ ИНФОРМАЦИЯ
        [HttpGet("debug")]
        public async Task<IActionResult> Debug()
        {
            try
            {
                var usersTotal = await _db.Users.CountAsync();
                var days = await _db.Days.ToListAsync();
                var preferencesTotal = await _db.Set<WordTimeReady>().CountAsync();
                var schedulesTotal = await _db.Schedules.CountAsync();
                var lastSchedules = await _db.Schedules.OrderByDescending(s => s.ID).Take(10).ToListAsync();

                return Ok(new
                {
                    totalUsers = usersTotal,
                    totalPreferences = preferencesTotal,
                    totalSchedules = schedulesTotal,
                    days = days.Select(d => new { d.ID, d.Name }).ToList(),
                    lastSchedules = lastSchedules.Select(s => new
                    {
                        s.ID,
                        s.ID_User,
                        s._Date,
                        start = s._Start.ToString(@"hh\:mm"),
                        end = s._End.ToString(@"hh\:mm"),
                        s.ID_Day
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }

    public class GenerateRequest
    {
        public DateTime StartDate { get; set; } = DateTime.Today;
        public int DaysCount { get; set; } = 7;
    }
}
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
                var preferencesCount = await _db.WordTimeReadies.CountAsync();

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
                return (new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0)); // По умолчанию

            var parts = timeString.Split('-');
            if (parts.Length == 2)
            {
                if (TimeSpan.TryParse(parts[0].Trim(), out var start) &&
                    TimeSpan.TryParse(parts[1].Trim(), out var end))
                {
                    return (start, end);
                }
            }

            return (new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0)); // По умолчанию
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
                Console.WriteLine($"===== ГЕНЕРАЦИЯ РАСПИСАНИЯ =====");
                Console.WriteLine($"Период: {request.StartDate:yyyy-MM-dd} - {request.StartDate.AddDays(request.DaysCount - 1):yyyy-MM-dd}");

                // 1. Загружаем данные
                var users = await _db.Users.ToListAsync();
                var daysList = await _db.Days.ToListAsync();
                var workTimePreferences = await _db.Set<WordTimeReady>()
     .FromSqlRaw("SELECT * FROM [WordTimeReady]")
     .ToListAsync();

                Console.WriteLine($"Пользователей: {users.Count}");
                Console.WriteLine($"Дней в справочнике: {daysList.Count}");
                Console.WriteLine($"Настроек рабочего времени: {workTimePreferences.Count}");

                if (!users.Any())
                    return BadRequest(new { error = "Нет пользователей" });
                if (!daysList.Any())
                    return BadRequest(new { error = "Нет данных в таблице Days" });

                // 2. Создаем словари
                var daysMap = daysList.ToDictionary(d => d.Name, d => d.ID);
                var preferencesByUser = workTimePreferences
                    .Where(p => p.ID_User.HasValue)
                    .ToDictionary(p => p.ID_User.Value);

                Console.WriteLine($"Пользователей с настройками времени: {preferencesByUser.Count}");

                // 3. Получаем начальный ID для новых записей
                var nextId = await GetNextScheduleId();
                int defaultDayId = daysList.First().ID;

                // 4. Генерируем смены
                var createdSchedules = new List<object>();
                int totalCreated = 0;

                for (int dayIndex = 0; dayIndex < request.DaysCount; dayIndex++)
                {
                    var currentDate = request.StartDate.AddDays(dayIndex);
                    var dayOfWeek = currentDate.DayOfWeek;
                    var dayName = dayOfWeek.ToString();

                    Console.WriteLine($"\n📅 День {dayIndex + 1}: {currentDate:yyyy-MM-dd} ({dayName})");

                    // Получаем ID дня из справочника
                    int dayId = daysMap.ContainsKey(dayName) ? daysMap[dayName] : defaultDayId;

                    // Берём пользователей, у которых есть настройки времени
                    var usersWithPreferences = users.Where(u => preferencesByUser.ContainsKey(u.ID)).ToList();

                    if (!usersWithPreferences.Any())
                    {
                        Console.WriteLine("  Нет пользователей с настройками времени, пропускаем день");
                        continue;
                    }

                    // Для каждого пользователя создаём смену с его временем
                    foreach (var user in usersWithPreferences)
                    {
                        try
                        {
                            var preference = preferencesByUser[user.ID];
                            var (startTime, endTime) = GetUserTimePreference(preference, dayOfWeek);

                            // Проверяем, что время указано (не пустое)
                            if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
                            {
                                Console.WriteLine($"  - User {user.ID} ({user.Surname}): нет времени на этот день, пропускаем");
                                continue;
                            }

                            var sql = @"INSERT INTO [Schedule] ([ID], [ID_User], [ID_Verification], [_Date], [_Start], [_End], [ID_Day]) 
                                        VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";

                            await _db.Database.ExecuteSqlRawAsync(sql,
                                nextId,
                                user.ID,
                                1, // ID_Verification всегда 1 (или любой существующий)
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
                                dayOfWeek = dayName,
                                start = startTime.ToString(@"hh\:mm"),
                                end = endTime.ToString(@"hh\:mm")
                            });

                            Console.WriteLine($"  ✓ ID={nextId}, {user.Surname} {user.Name}: {startTime:hh\\:mm} - {endTime:hh\\:mm}");

                            totalCreated++;
                            nextId++;
                        }
                        catch (Exception insertEx)
                        {
                            Console.WriteLine($"  ✗ Ошибка для User {user.ID}: {insertEx.Message}");
                            if (insertEx.InnerException != null)
                                Console.WriteLine($"    Inner: {insertEx.InnerException.Message}");
                        }
                    }
                }

                Console.WriteLine($"\n✅ Всего создано: {totalCreated} смен");

                return Ok(new
                {
                    message = $"Успешно создано {totalCreated} смен",
                    count = totalCreated,
                    startDate = request.StartDate,
                    endDate = request.StartDate.AddDays(request.DaysCount - 1),
                    daysCount = request.DaysCount,
                    schedules = createdSchedules
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
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
                var preferences = await _db.WordTimeReadies
                    .Where(p => p.ID_User.HasValue)
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
                    .OrderBy(s => s._Date)
                    .ThenBy(s => s.ID_User)
                    .ToListAsync();

                // Добавляем имена пользователей для удобства
                var userIds = schedules.Select(s => s.ID_User).Distinct();
                var users = await _db.Users.Where(u => userIds.Contains(u.ID)).ToDictionaryAsync(u => u.ID);

                var result = schedules.Select(s => new
                {
                    id = s.ID,
                    userId = s.ID_User,
                    userName = users.ContainsKey(s.ID_User)
                        ? $"{users[s.ID_User].Surname} {users[s.ID_User].Name}"
                        : "Unknown",
                    date = s._Date.ToString("yyyy-MM-dd"),
                    dayOfWeek = s._Date.DayOfWeek.ToString(),
                    start = s._Start.ToString(@"hh\:mm"),
                    end = s._End.ToString(@"hh\:mm"),
                    dayId = s.ID_Day
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ОЧИСТИТЬ РАСПИСАНИЕ
        [HttpDelete("clear")]
        public async Task<IActionResult> Clear([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            try
            {
                var sql = @"DELETE FROM [Schedule] WHERE [_Date] >= @p0 AND [_Date] <= @p1";
                var deleted = await _db.Database.ExecuteSqlRawAsync(sql, fromDate, toDate);

                return Ok(new { message = $"Удалено {deleted} записей", fromDate, toDate });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ОТЛАДОЧНАЯ ИНФОРМАЦИЯ
        [HttpGet("debug")]
        public async Task<IActionResult> Debug()
        {
            try
            {
                var users = await _db.Users.Take(3).ToListAsync();
                var days = await _db.Days.ToListAsync();
                var preferences = await _db.WordTimeReadies.Where(p => p.ID_User.HasValue).Take(3).ToListAsync();
                var schedules = await _db.Schedules.OrderByDescending(s => s.ID).Take(5).ToListAsync();
                var maxScheduleId = await _db.Schedules.MaxAsync(s => (int?)s.ID) ?? 0;

                return Ok(new
                {
                    maxScheduleId = maxScheduleId,
                    nextScheduleId = maxScheduleId + 1,
                    sampleUsers = users.Select(u => new { u.ID, u.Name, u.Surname }),
                    days = days.Select(d => new { d.ID, d.Name }),
                    samplePreferences = preferences.Select(p => new
                    {
                        p.ID,
                        userId = p.ID_User,
                        p.Monday,
                        p.Tuesday,
                        p.Wednesday,
                        p.Thursday,
                        p.Friday
                    }),
                    lastSchedules = schedules.Select(s => new
                    {
                        s.ID,
                        s.ID_User,
                        s._Date,
                        s._Start,
                        s._End,
                        s.ID_Day
                    })
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
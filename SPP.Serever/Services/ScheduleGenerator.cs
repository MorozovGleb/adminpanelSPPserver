using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using SPP.Serever.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SPP.Serever.Services
{
    // Вспомогательные классы
    public class TimeSlot
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public int VerificationId { get; set; }
        public int RequiredStaff { get; set; }
    }

    public class ShiftDefinition
    {
        public string Name { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public int DayOffset { get; set; }
    }

    public class SolverInput
    {
        public List<SolverUser> Users { get; set; }
        public List<SolverRequirement> Requirements { get; set; }
        public List<ShiftDefinition> Shifts { get; set; }
    }

    public class SolverUser
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public List<int> AvailableVerifications { get; set; }
        public List<ExistingShift> ExistingShifts { get; set; }
        public Dictionary<DayOfWeek, List<TimeRange>> Preferences { get; set; }
    }

    public class SolverRequirement
    {
        public DateTime Date { get; set; }
        public int VerificationId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int RequiredStaff { get; set; }
    }

    public class ExistingShift
    {
        public DateTime Date { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }

    public class TimeRange
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }

    public class ScheduleAssignment
    {
        public DateTime Date { get; set; }
        public int VerificationId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    // Основной класс генератора
    public class ScheduleGenerator
    {
        private readonly SppDbContext _db;

        private readonly List<ShiftDefinition> _shifts = new()
        {
            new ShiftDefinition { Name = "Утро", Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(16), DayOffset = 0 },
            new ShiftDefinition { Name = "Вечер", Start = TimeSpan.FromHours(16), End = TimeSpan.FromHours(0), DayOffset = 0 },
            new ShiftDefinition { Name = "Ночь", Start = TimeSpan.FromHours(0), End = TimeSpan.FromHours(8), DayOffset = 1 }
        };

        public ScheduleGenerator(SppDbContext db)
        {
            _db = db;
        }

        public List<Schedule> GenerateSchedule(DateTime startDate, int daysCount)
        {
            // Загружаем данные
            var users = _db.Users.ToList();
            var roles = _db.Roles.ToDictionary(r => r.ID);
            var verifications = _db.Verifications.ToList();

            var confirmations = _db.ConfirmationVerifications.ToList();
            var confirmationsByUser = confirmations
                .GroupBy(c => c.ID_User)
                .ToDictionary(g => g.Key, g => g.Select(c => c.ID_Verification).ToList());

            var preferences = _db.WordTimeReadies.ToList();
            var preferencesByUser = preferences
                .Where(p => p.ID_User.HasValue)
                .ToDictionary(p => p.ID_User.Value);

            var existingSchedules = _db.Schedules
                .Where(s => s._Date >= startDate && s._Date < startDate.AddDays(daysCount))
                .ToList();

            var schedulesByUser = existingSchedules
                .GroupBy(s => s.ID_User)
                .ToDictionary(g => g.Key, g => g.ToList());

            var daysMap = _db.Days.ToDictionary(d => d.Name, d => d.ID);

            // Генерируем требования
            var requirements = GetShiftRequirements(startDate, daysCount, verifications);

            // Конвертируем в формат солвера
            var solverInput = MapToSolverInput(
                users, roles, confirmationsByUser, preferencesByUser,
                schedulesByUser, requirements, startDate, daysCount);

            // Решаем
            var solution = Solve(solverInput);

            // Конвертируем обратно
            return CreateScheduleEntities(solution, users, daysMap, startDate);
        }

        private List<TimeSlot> GetShiftRequirements(DateTime startDate, int daysCount, List<Verification> verifications)
        {
            var requirements = new List<TimeSlot>();

            for (int d = 0; d < daysCount; d++)
            {
                foreach (var verification in verifications)
                {
                    foreach (var shift in _shifts)
                    {
                        requirements.Add(new TimeSlot
                        {
                            VerificationId = verification.ID,
                            Start = shift.Start,
                            End = shift.End,
                            RequiredStaff = 1
                        });
                    }
                }
            }

            return requirements;
        }

        private SolverInput MapToSolverInput(
            List<User> users,
            Dictionary<int, Role> roles,
            Dictionary<int, List<int>> confirmationsByUser,
            Dictionary<int, WordTimeReady> preferencesByUser,
            Dictionary<int, List<Schedule>> schedulesByUser,
            List<TimeSlot> requirements,
            DateTime startDate,
            int daysCount)
        {
            var input = new SolverInput
            {
                Users = new List<SolverUser>(),
                Requirements = new List<SolverRequirement>(),
                Shifts = _shifts
            };

            foreach (var user in users)
            {
                var solverUser = new SolverUser
                {
                    Id = user.ID,
                    RoleId = user.ID_Role,
                    RoleName = roles.ContainsKey(user.ID_Role) ? roles[user.ID_Role].Name : "Unknown",
                    AvailableVerifications = confirmationsByUser.ContainsKey(user.ID)
                        ? confirmationsByUser[user.ID]
                        : new List<int>(),
                    ExistingShifts = schedulesByUser.ContainsKey(user.ID)
                        ? schedulesByUser[user.ID]
                            .Select(s => new ExistingShift
                            {
                                Date = s._Date,
                                Start = s._Start,
                                End = s._End
                            })
                            .ToList()
                        : new List<ExistingShift>(),
                    Preferences = preferencesByUser.ContainsKey(user.ID)
                        ? ParsePreferences(preferencesByUser[user.ID])
                        : new Dictionary<DayOfWeek, List<TimeRange>>()
                };

                input.Users.Add(solverUser);
            }

            int dayIndex = 0;
            foreach (var req in requirements)
            {
                var solverReq = new SolverRequirement
                {
                    Date = startDate.AddDays(dayIndex % daysCount).Date,
                    VerificationId = req.VerificationId,
                    StartTime = req.Start,
                    EndTime = req.End,
                    RequiredStaff = req.RequiredStaff
                };

                input.Requirements.Add(solverReq);
                dayIndex++;
            }

            return input;
        }

        private Dictionary<DayOfWeek, List<TimeRange>> ParsePreferences(WordTimeReady preference)
        {
            var result = new Dictionary<DayOfWeek, List<TimeRange>>();

            ParseDayPreference(result, DayOfWeek.Monday, preference.Monday);
            ParseDayPreference(result, DayOfWeek.Tuesday, preference.Tuesday);
            ParseDayPreference(result, DayOfWeek.Wednesday, preference.Wednesday);
            ParseDayPreference(result, DayOfWeek.Thursday, preference.Thursday);
            ParseDayPreference(result, DayOfWeek.Friday, preference.Friday);
            ParseDayPreference(result, DayOfWeek.Saturday, preference.Saturday);
            ParseDayPreference(result, DayOfWeek.Sunday, preference.Sunday);

            return result;
        }

        private void ParseDayPreference(Dictionary<DayOfWeek, List<TimeRange>> result, DayOfWeek day, string timeString)
        {
            if (string.IsNullOrEmpty(timeString)) return;

            var ranges = new List<TimeRange>();
            var parts = timeString.Split(',');

            foreach (var part in parts)
            {
                var times = part.Trim().Split('-');
                if (times.Length == 2)
                {
                    if (TimeSpan.TryParse(times[0].Trim(), out var start) &&
                        TimeSpan.TryParse(times[1].Trim(), out var end))
                    {
                        ranges.Add(new TimeRange { Start = start, End = end });
                    }
                }
            }

            if (ranges.Any())
            {
                result[day] = ranges;
            }
        }

        private Dictionary<int, List<ScheduleAssignment>> Solve(SolverInput input)
        {
            CpModel model = new CpModel();

            var x = new Dictionary<(int userId, DateTime date, int verificationId), BoolVar>();

            foreach (var user in input.Users)
            {
                foreach (var req in input.Requirements)
                {
                    if (!user.AvailableVerifications.Contains(req.VerificationId))
                        continue;

                    if (user.ExistingShifts.Any(s =>
                        s.Date.Date == req.Date.Date &&
                        Overlaps(s.Start, s.End, req.StartTime, req.EndTime)))
                        continue;

                    var key = (user.Id, req.Date.Date, req.VerificationId);
                    x[key] = model.NewBoolVar($"user_{user.Id}_date_{req.Date:yyyyMMdd}_ver_{req.VerificationId}");
                }
            }

            foreach (var req in input.Requirements)
            {
                var availableWorkers = new List<BoolVar>();

                foreach (var user in input.Users)
                {
                    var key = (user.Id, req.Date.Date, req.VerificationId);
                    if (x.ContainsKey(key))
                    {
                        availableWorkers.Add(x[key]);
                    }
                }

                if (availableWorkers.Any())
                {
                    model.Add(LinearExpr.Sum(availableWorkers) == req.RequiredStaff);
                }
            }

            foreach (var user in input.Users)
            {
                var shiftsByDay = x.Keys
                    .Where(k => k.userId == user.Id)
                    .GroupBy(k => k.date.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var dayShifts in shiftsByDay)
                {
                    var dayVars = dayShifts.Value.Select(k => x[k]).ToList();
                    if (dayVars.Any())
                    {
                        model.Add(LinearExpr.Sum(dayVars) <= 1);
                    }
                }
            }

            CpSolver solver = new CpSolver();
            solver.StringParameters = "max_time_in_seconds:30.0";
            var status = solver.Solve(model);

            if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
            {
                throw new Exception($"Не удалось найти решение. Статус: {status}");
            }

            var result = new Dictionary<int, List<ScheduleAssignment>>();

            foreach (var kvp in x)
            {
                if (solver.Value(kvp.Value) == 1)
                {
                    if (!result.ContainsKey(kvp.Key.userId))
                    {
                        result[kvp.Key.userId] = new List<ScheduleAssignment>();
                    }

                    var req = input.Requirements
                        .First(r => r.Date.Date == kvp.Key.date &&
                                   r.VerificationId == kvp.Key.verificationId);

                    result[kvp.Key.userId].Add(new ScheduleAssignment
                    {
                        Date = kvp.Key.date,
                        VerificationId = kvp.Key.verificationId,
                        StartTime = req.StartTime,
                        EndTime = req.EndTime
                    });
                }
            }

            return result;
        }

        private List<Schedule> CreateScheduleEntities(
            Dictionary<int, List<ScheduleAssignment>> solution,
            List<User> users,
            Dictionary<string, int> daysMap,
            DateTime startDate)
        {
            var schedules = new List<Schedule>();

            foreach (var userAssignments in solution)
            {
                var user = users.First(u => u.ID == userAssignments.Key);

                foreach (var assignment in userAssignments.Value)
                {
                    var dayName = assignment.Date.DayOfWeek.ToString();

                    var schedule = new Schedule
                    {
                        ID_User = user.ID,
                        ID_Verification = assignment.VerificationId,
                        _Date = assignment.Date,
                        _Start = assignment.StartTime,
                        _End = assignment.EndTime,
                        ID_Day = daysMap[dayName]
                    };

                    schedules.Add(schedule);
                }
            }

            return schedules;
        }

        private bool Overlaps(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
        {
            return start1 < end2 && start2 < end1;
        }
    }
}
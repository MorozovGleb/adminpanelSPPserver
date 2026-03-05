using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SPP.Serever.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ScheduleRepository _repo;

        public ScheduleController(IHttpClientFactory factory, IConfiguration config, ScheduleRepository repo)
        {
            _httpClient = factory.CreateClient();
            _config = config;
            _repo = repo;
        }

        [HttpGet("stream")]
        public async Task StreamSchedule()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");

            var apiKey = _config["YandexGPT:ApiKey"];
            var folderId = _config["YandexGPT:FolderId"];

            var workers = await _repo.GetWorkersAsync();

            var groupedWorkers = workers
                .GroupBy(w => new { w.WorkerId, w.WorkerName })
                .Select(g => new
                {
                    Id = g.Key.WorkerId,
                    Name = g.Key.WorkerName,
                    Positions = g.Select(x => x.Code).Distinct().ToList()
                })
                .ToList();

            var workersJson = JsonSerializer.Serialize(groupedWorkers,
                new JsonSerializerOptions { WriteIndented = true });

            string prompt = $@"
Ты менеджер ресторана и составляешь расписание.

Вот сотрудники:

{workersJson}

Правила:

1. Работать можно только на обученных позициях
2. Утром один DLK и один MR
3. Вечером один DLK и один MR
4. Нельзя работать на двух позициях одновременно

Формат:

ID Имя
День Дата Начало Окончание Часы Позиция

Ответ только расписанием.
";

            var body = new
            {
                modelUri = $"gpt://{folderId}/yandexgpt-lite",
                completionOptions = new
                {
                    stream = true,
                    temperature = 0.3,
                    maxTokens = 15000
                },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        text = prompt
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://llm.api.cloud.yandex.net/foundationModels/v1/completion");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Api-Key", apiKey);

            request.Content =
                new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    await Response.WriteAsync(line + "\n");
                    await Response.Body.FlushAsync();
                }
            }
        }
    }
}
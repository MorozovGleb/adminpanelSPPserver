
using SPP.Serever.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


namespace SPP.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ChatController(IHttpClientFactory factory, IConfiguration configuration)
        {
            _httpClient = factory.CreateClient();
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            var apiKey = _configuration["YandexGPT:ApiKey"];
            var folderId = _configuration["YandexGPT:FolderId"];

            var requestBody = new
            {
                modelUri = $"gpt://{folderId}/yandexgpt-lite",
                completionOptions = new
                {
                    stream = false,
                    temperature = 0.6,
                    maxTokens = 1000
                },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        text = request.Message
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "https://llm.api.cloud.yandex.net/foundationModels/v1/completion");

            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Api-Key", apiKey);

            httpRequest.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return BadRequest(responseJson);

            using JsonDocument doc = JsonDocument.Parse(responseJson);

            string answer = doc.RootElement
                .GetProperty("result")
                .GetProperty("alternatives")[0]
                .GetProperty("message")
                .GetProperty("text")
                .GetString();

            return Ok(answer);
        }
    }
}
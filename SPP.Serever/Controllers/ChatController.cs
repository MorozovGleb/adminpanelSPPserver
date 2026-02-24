using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SPP.Serever.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SPP.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly string _connectionString =
            "Server=DESKTOP-20TGD0H\\\\SQLEXPRESS;Database=SPP_V4;User Id=spp_user;Password=SppStrong_123!;TrustServerCertificate=True;";

        [HttpPost("SaveMessage")]
        public async Task<IActionResult> SaveMessage([FromBody] ChatMessageModel message)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqlCommand(@"
                INSERT INTO ChatMessage (SessionId, Role, Content)
                VALUES (@SessionId, @Role, @Content)", connection);

            command.Parameters.AddWithValue("@SessionId", message.SessionId);
            command.Parameters.AddWithValue("@Role", message.Role);
            command.Parameters.AddWithValue("@Content", message.Content);

            await command.ExecuteNonQueryAsync();

            return Ok();
        }

        [HttpGet("GetMessages/{sessionId}")]
        public async Task<List<ChatMessageModel>> GetMessages(Guid sessionId)
        {
            var list = new List<ChatMessageModel>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqlCommand(@"
                SELECT Id, SessionId, Role, Content, CreatedAt
                FROM ChatMessage
                WHERE SessionId = @SessionId
                ORDER BY CreatedAt", connection);

            command.Parameters.AddWithValue("@SessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ChatMessageModel
                {
                    Id = reader.GetInt32(0),
                    SessionId = reader.GetGuid(1),
                    Role = reader.GetString(2),
                    Content = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4)
                });
            }

            return list;
        }
    }
}
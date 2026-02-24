namespace SPP.Serever.Models
{
    public class ChatMessageModel
    {
        public int Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

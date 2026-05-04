using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPP.Serever.Models
{
    public class WordTimeReady
    {
        [Key]
        public int ID { get; set; }

        [ForeignKey("User")]
        public int? ID_User { get; set; }

        public string? Monday { get; set; }
        public string? Tuesday { get; set; }
        public string? Wednesday { get; set; }
        public string? Thursday { get; set; }
        public string? Friday { get; set; }
        public string? Saturday { get; set; }
        public string? Sunday { get; set; }

        public virtual User? User { get; set; }
    }
}

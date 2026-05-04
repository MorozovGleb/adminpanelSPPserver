using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPP.Serever.Models
{
    [Table("Days")]
    public class Day
    {
        [Key]
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
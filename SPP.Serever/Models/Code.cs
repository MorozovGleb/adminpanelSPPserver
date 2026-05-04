using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPP.Serever.Models
{
    [Table("Code")]
    public class Code
    {
        [Key]
        public int ID { get; set; }
        public string _Name { get; set; }
    }
}
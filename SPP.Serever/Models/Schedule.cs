using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SPP.Serever.Models
{
    [Table("Schedule")]
    public class Schedule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ВАЖНО!
        public int ID { get; set; }

        public int ID_User { get; set; }
        public int ID_Verification { get; set; }
        public DateTime _Date { get; set; }
        public TimeSpan _Start { get; set; }
        public TimeSpan _End { get; set; }
        public int ID_Day { get; set; }
    }

}

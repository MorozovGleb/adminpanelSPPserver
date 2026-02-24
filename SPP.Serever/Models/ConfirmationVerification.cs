using System.ComponentModel.DataAnnotations.Schema;

namespace SPP.Serever.Models
{
    [Table("Confirmation_verification")]
    public class ConfirmationVerification
    {
        public int ID { get; set; }
        public int ID_User { get; set; }
        public int ID_Verification { get; set; }
        public DateTime _Date { get; set; }
    }
}

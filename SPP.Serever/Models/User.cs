using System.ComponentModel.DataAnnotations.Schema;

namespace SPP.Serever.Models
{
    public class User
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Phone_number { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }

        public int ID_Role { get; set; }

        [ForeignKey("ID_Role")] // Указываем, какое свойство является внешним ключом
        public Role Role { get; set; }
    }

}

namespace SPP.Serever.Models;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Verification")]
public class Verification
{
    public int ID { get; set; }
    public string Name { get; set; }
}
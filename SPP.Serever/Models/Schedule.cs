namespace SPP.Serever.Models
{
    public class Schedule
    {
        public int ID { get; set; }
        public int ID_User { get; set; }
        public User User { get; set; }

        public DateTime _Date { get; set; }
        public TimeSpan _Start { get; set; }
        public TimeSpan _End { get; set; }
    }

}

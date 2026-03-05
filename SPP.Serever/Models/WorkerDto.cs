namespace SPP.Serever.Models
{
    public class WorkerDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> TrainedPositions { get; set; }
    }
}

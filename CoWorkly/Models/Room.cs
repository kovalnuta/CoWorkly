namespace CoWorkly.Models
{
    public class Room
    {
        public int Id { get; set; }
        public int FloorId { get; set; }
        public Floor? Floor { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; 
        public int Capacity { get; set; }
    }
}
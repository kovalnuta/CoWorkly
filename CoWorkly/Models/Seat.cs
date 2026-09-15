namespace CoWorkly.Models
{
    public class Seat
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public Room? Room { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string Number { get; set; } = string.Empty; 
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace CoWorkly.Models
{
    class Booking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User ? User { get; set; }
        public int WorkspaceId { get; set; }
        public Workspace ? Workspace { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreatedAt{ get; set; } = DateTime.Now;
         
    }
}

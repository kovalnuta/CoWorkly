using System;
using System.Collections.Generic;
using System.Text;

namespace CoWorkly.Models
{
    class Workspace
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Capacity {  get; set; }
        public decimal PricePerHour { get; set; }

    }
}

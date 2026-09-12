using System;
using System.Collections.Generic;
using System.Text;

namespace CoWorkly.Models
{
    class User
    {
        public int Id { get; set; } 
        public String Username { get; set; }
        public String Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;


    }
}

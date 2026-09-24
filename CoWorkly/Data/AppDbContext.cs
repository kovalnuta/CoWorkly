using Microsoft.EntityFrameworkCore;
using CoWorkly.Models;

namespace CoWorkly.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Floor> Floors { get; set; } = null!;
        public DbSet<Room> Rooms { get; set; } = null!;
        public DbSet<Seat> Seats { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Все таблицы — в схеме CoWorkly
            modelBuilder.HasDefaultSchema("CoWorkly");

            modelBuilder.Entity<Room>()
                .HasOne(r => r.Floor)
                .WithMany()
                .HasForeignKey(r => r.FloorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Room)
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Seat)
                .WithMany()
                .HasForeignKey(b => b.SeatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
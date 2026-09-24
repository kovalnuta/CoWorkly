using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoWorkly.Data;
using CoWorkly.Models;
using CoWorkly.Services;

namespace CoWorkly
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        // Подключение к PostgreSQL от имени postgres.
        // Замени ***** на реальный пароль пользователя postgres.
        private const string ConnectionString =
            "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=kasqu62#BK%71;Search Path=CoWorkly,public;Include Error Detail=true";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            using (var scope = ServiceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // ВАЖНО: только EnsureCreated, без EnsureDeleted.
                // Таблицы создадутся один раз. Данные сохраняются.
                context.Database.EnsureCreated();

                SeedData(context);
            }

            new LoginWindow().Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(ConnectionString));

            services.AddScoped<AuthService>();
        }

        private static void SeedData(AppDbContext db)
        {
            if (db.Users.Any()) return;

            db.Users.AddRange(
                new User
                {
                    Username = "Anna",
                    Email = "anna@coworkly.com",
                    Role = "Admin",
                    PasswordHash = PasswordHasher.Hash("Passw0rd!")
                },
                new User
                {
                    Username = "Kirill",
                    Email = "kirill@coworkly.com",
                    Role = "Client",
                    PasswordHash = PasswordHasher.Hash("Passw0rd!")
                }
            );
            db.SaveChanges();

            var floor1 = new Floor { Number = 1, Description = "Open space и переговорные" };
            var floor2 = new Floor { Number = 2, Description = "Рабочие зоны" };
            var floor3 = new Floor { Number = 3, Description = "VIP-зона и кухня" };
            db.Floors.AddRange(floor1, floor2, floor3);
            db.SaveChanges();

            var room101 = new Room { FloorId = floor1.Id, Name = "101: Open Space А", Type = "open_space", Capacity = 12 };
            var room102 = new Room { FloorId = floor1.Id, Name = "102: Переговорная «Токио»", Type = "meeting_room", Capacity = 6 };
            var room103 = new Room { FloorId = floor1.Id, Name = "103: Phone Booth", Type = "phone_booth", Capacity = 2 };

            var room201 = new Room { FloorId = floor2.Id, Name = "201: Open Space Б", Type = "open_space", Capacity = 16 };
            var room202 = new Room { FloorId = floor2.Id, Name = "202: Переговорная «Париж»", Type = "meeting_room", Capacity = 4 };

            var room301 = new Room { FloorId = floor3.Id, Name = "301: VIP-зона", Type = "open_space", Capacity = 8 };
            var room302 = new Room { FloorId = floor3.Id, Name = "302: Кухня-лаунж", Type = "kitchen", Capacity = 10 };

            db.Rooms.AddRange(room101, room102, room103, room201, room202, room301, room302);
            db.SaveChanges();

            AddSeats(db, room101.Id, rows: 3, columns: 4, prefix: "A");
            AddSeats(db, room102.Id, rows: 2, columns: 3, prefix: "T");
            AddSeats(db, room103.Id, rows: 1, columns: 2, prefix: "PB");
            AddSeats(db, room201.Id, rows: 4, columns: 4, prefix: "B");
            AddSeats(db, room202.Id, rows: 2, columns: 2, prefix: "P");
            AddSeats(db, room301.Id, rows: 2, columns: 4, prefix: "V");
            AddSeats(db, room302.Id, rows: 2, columns: 5, prefix: "K");

            db.SaveChanges();
        }

        private static void AddSeats(AppDbContext db, int roomId, int rows, int columns, string prefix)
        {
            for (int r = 1; r <= rows; r++)
            {
                for (int c = 1; c <= columns; c++)
                {
                    db.Seats.Add(new Seat
                    {
                        RoomId = roomId,
                        Row = r,
                        Column = c,
                        Number = $"{prefix}{(r - 1) * columns + c}"
                    });
                }
            }
        }
    }
}
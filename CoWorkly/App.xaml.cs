using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CoWorkly.Data;
using CoWorkly.Models;
using CoWorkly.Services;

namespace CoWorkly
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = BuildConfiguration(AppContext.BaseDirectory);
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                ShowError("Не задана строка подключения ConnectionStrings:DefaultConnection в appsettings.json.");
                Shutdown(3);
                return;
            }

            // База в Docker: проверить, при необходимости запустить или пересоздать
            if (!EnsureDatabase(connectionString))
            {
                Shutdown(3);
                return;
            }

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            ConfigureServices(services, configuration);
            ServiceProvider = services.BuildServiceProvider();

            try
            {
                using var scope = ServiceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                context.Database.Migrate();
                SeedData(context);
            }
            catch (Exception ex)
            {
                ShowError("Не удалось подготовить базу данных (миграции или начальные данные):\n" + ex.Message);
                Shutdown(3);
                return;
            }

            new LoginWindow().Show();
        }

        /// <summary>
        /// Конфигурация: appsettings.json из репозитория (настройки Docker-базы по умолчанию)
        /// и необязательный appsettings.Local.json для своих локальных настроек (в git не попадает).
        /// Старый appsettings_new.json больше не читается: из-за устаревших копий с портом 5432
        /// и другим паролем программа и падала с 28P01.
        /// </summary>
        public static IConfiguration BuildConfiguration(string basePath) =>
            new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();

        /// <summary>
        /// Проверяет базу; если она не запущена — запускает Docker и контейнер,
        /// если отклоняет пароль — предлагает пересоздать локальную базу.
        /// </summary>
        private static bool EnsureDatabase(string connectionString)
        {
            var status = DatabaseBootstrap.Check(connectionString, out var details);
            if (status == DbStatus.Ok)
                return true;

            var composeDir = DatabaseBootstrap.FindComposeDirectory();

            if (status == DbStatus.Unreachable && composeDir != null)
            {
                string? problem;
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    problem = DatabaseBootstrap.StartContainer(composeDir);
                    status = problem == null
                        ? DatabaseBootstrap.WaitUntilReady(connectionString, TimeSpan.FromSeconds(30), out details)
                        : DbStatus.Other;
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }

                if (problem != null)
                {
                    ShowError(problem);
                    return false;
                }
            }

            if (status == DbStatus.Rejected && composeDir != null)
            {
                var answer = MessageBox.Show(
                    "База данных отклонила подключение:\n" + details + "\n\n" +
                    "Обычно это значит, что локальная база в Docker создана раньше с другим паролем " +
                    "(например, из другой копии проекта).\n\n" +
                    "Пересоздать локальную базу? Все локальные данные (пользователи, брони) будут удалены, " +
                    "учётные записи Anna и Kirill создадутся заново.",
                    "CoWorkly — база данных",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (answer == MessageBoxResult.Yes)
                {
                    string? problem;
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        problem = DatabaseBootstrap.ResetDatabase(composeDir);
                        status = problem == null
                            ? DatabaseBootstrap.WaitUntilReady(connectionString, TimeSpan.FromSeconds(30), out details)
                            : DbStatus.Other;
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }

                    if (problem != null)
                    {
                        ShowError(problem);
                        return false;
                    }
                }
            }

            if (status == DbStatus.Ok)
                return true;

            ShowError(status switch
            {
                DbStatus.Unreachable =>
                    "База данных не отвечает.\n\nЗапустите Docker Desktop и в папке проекта выполните:\ndocker compose up -d\n\n" + details,
                DbStatus.Rejected =>
                    "База данных отклонила пользователя или пароль.\n\nВ папке проекта выполните:\n" +
                    "docker rm -f coworkly_db\ndocker compose down -v\ndocker compose up -d\n\n" + details,
                _ => "Не удалось подключиться к базе данных:\n" + details
            });
            return false;
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "CoWorkly — база данных недоступна", MessageBoxButton.OK, MessageBoxImage.Error);

        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

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
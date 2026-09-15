using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoWorkly.Data;
using CoWorkly.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoWorkly.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty] private int _currentLevel = 1; 

        [ObservableProperty] private Floor? _selectedFloor;
        [ObservableProperty] private Room? _selectedRoom;

        [ObservableProperty] private ObservableCollection<Floor> _floors = new();
        [ObservableProperty] private ObservableCollection<Room> _rooms = new();
        [ObservableProperty] private ObservableCollection<SeatDisplay> _seats = new();

        // Статус
        [ObservableProperty] private string _currentUserName = "Гость";
        [ObservableProperty] private string _statusMessage = "Выберите этаж";
        [ObservableProperty] private string _breadcrumb = "🏢 Коворкинг";

        // Максимальные размеры для сетки мест
        public int MaxRow { get; private set; } = 1;
        public int MaxColumn { get; private set; } = 1;

        public MainViewModel()
        {
            LoadCurrentUser();
            LoadFloors();
        }

        private void LoadCurrentUser()
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = context.Users.FirstOrDefault(u => u.Id == App.CurrentUserId);
            CurrentUserName = user != null ? $"👤 {user.Username} ({user.Role})" : "Гость";
        }

        private void LoadFloors()
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var floors = context.Floors.OrderBy(f => f.Number).ToList();
            Floors.Clear();
            foreach (var f in floors) Floors.Add(f);
            CurrentLevel = 1;
            Breadcrumb = "🏢 Коворкинг → Выберите этаж";
            StatusMessage = $"Доступно этажей: {floors.Count}";
        }

        [RelayCommand]
        private void SelectFloor(Floor floor)
        {
            SelectedFloor = floor;
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rooms = context.Rooms
                .Where(r => r.FloorId == floor.Id)
                .OrderBy(r => r.Name)
                .ToList();
            Rooms.Clear();
            foreach (var r in rooms) Rooms.Add(r);
            CurrentLevel = 2;
            Breadcrumb = $"🏢 Коворкинг → Этаж {floor.Number}";
            StatusMessage = $"Комнат на этаже: {rooms.Count}";
        }

        [RelayCommand]
        private void SelectRoom(Room room)
        {
            SelectedRoom = room;
            LoadSeats(room.Id);
            CurrentLevel = 3;
            Breadcrumb = $"🏢 Коворкинг → Этаж {SelectedFloor?.Number} → {room.Name}";
        }

        private void LoadSeats(int roomId)
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seats = context.Seats
                .Where(s => s.RoomId == roomId)
                .OrderBy(s => s.Row).ThenBy(s => s.Column)
                .ToList();

            var activeBookings = context.Bookings
                .Where(b => b.Seat.RoomId == roomId &&
                            b.StartTime <= DateTime.Now &&
                            b.EndTime > DateTime.Now)
                .Include(b => b.User)
                .ToList();

            Seats.Clear();
            MaxRow = seats.Any() ? seats.Max(s => s.Row) : 1;
            MaxColumn = seats.Any() ? seats.Max(s => s.Column) : 1;

            foreach (var seat in seats)
            {
                var booking = activeBookings.FirstOrDefault(b => b.SeatId == seat.Id);
                var display = new SeatDisplay
                {
                    SeatId = seat.Id,
                    SeatNumber = seat.Number,
                    Row = seat.Row,
                    Column = seat.Column
                };

                if (booking == null)
                {
                    display.Status = SeatStatus.Free;
                    display.DisplayText = seat.Number;
                }
                else if (booking.UserId == App.CurrentUserId)
                {
                    display.Status = SeatStatus.MyBooking;
                    display.DisplayText = "✓";
                    display.Tooltip = $"Ваша бронь до {booking.EndTime:HH:mm}";
                }
                else
                {
                    display.Status = SeatStatus.Busy;
                    display.DisplayText = booking.User?.Username.Substring(0, 1) ?? "?";
                    display.Tooltip = $"Занято: {booking.User?.Username} до {booking.EndTime:HH:mm}";
                }

                Seats.Add(display);
            }

            var freeCount = Seats.Count(s => s.Status == SeatStatus.Free);
            StatusMessage = $"Свободно мест: {freeCount} из {Seats.Count}";
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentLevel == 3)
            {
                SelectFloor(SelectedFloor!);
            }
            else if (CurrentLevel == 2)
            {
                LoadFloors();
            }
        }

        [RelayCommand]
        private void SelectSeat(SeatDisplay seat)
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeBooking = context.Bookings
                .FirstOrDefault(b => b.SeatId == seat.SeatId &&
                                     b.StartTime <= DateTime.Now &&
                                     b.EndTime > DateTime.Now);

            if (activeBooking != null)
            {
                if (activeBooking.UserId == App.CurrentUserId)
                {
                    context.Bookings.Remove(activeBooking);
                    context.SaveChanges();
                    StatusMessage = $"✓ Бронь места {seat.SeatNumber} отменена";
                }
                else
                {
                    StatusMessage = $"⛔ Место {seat.SeatNumber} занято пользователем {activeBooking.User?.Username}";
                    return;
                }
            }
            else
            {
                var newBooking = new Booking
                {
                    UserId = App.CurrentUserId,
                    SeatId = seat.SeatId,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddHours(2)
                };
                context.Bookings.Add(newBooking);
                context.SaveChanges();
                StatusMessage = $"✓ Место {seat.SeatNumber} забронировано до {newBooking.EndTime:HH:mm}";
            }

            if (SelectedRoom != null) LoadSeats(SelectedRoom.Id);
        }

        [RelayCommand]
        private void CreateTestData()
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (context.Floors.Any())
            {
                StatusMessage = "Тестовые данные уже созданы";
                return;
            }

            context.Users.AddRange(
                new User { Username = "Анна", Email = "anna@coworkly.com", Role = "Admin" },
                new User { Username = "Кирилл", Email = "kirill@coworkly.com", Role = "Client" }
            );

            var floor1 = new Floor { Number = 1, Description = "Open space и переговорные" };
            var floor2 = new Floor { Number = 2, Description = "Рабочие зоны" };
            var floor3 = new Floor { Number = 3, Description = "VIP-зона и кухня" };
            context.Floors.AddRange(floor1, floor2, floor3);
            context.SaveChanges();

            var room101 = new Room { FloorId = floor1.Id, Name = "101: Open Space А", Type = "open_space", Capacity = 12 };
            var room102 = new Room { FloorId = floor1.Id, Name = "102: Переговорная «Токио»", Type = "meeting_room", Capacity = 6 };
            var room103 = new Room { FloorId = floor1.Id, Name = "103: Phone Booth", Type = "phone_booth", Capacity = 2 };
            context.Rooms.AddRange(room101, room102, room103);
            context.SaveChanges();

            AddSeats(context, room101.Id, rows: 3, columns: 4, prefix: "A");
            AddSeats(context, room102.Id, rows: 2, columns: 3, prefix: "T");
            AddSeats(context, room103.Id, rows: 1, columns: 2, prefix: "PB");

            var room201 = new Room { FloorId = floor2.Id, Name = "201: Open Space Б", Type = "open_space", Capacity = 16 };
            var room202 = new Room { FloorId = floor2.Id, Name = "202: Переговорная «Париж»", Type = "meeting_room", Capacity = 4 };
            context.Rooms.AddRange(room201, room202);
            context.SaveChanges();

            AddSeats(context, room201.Id, rows: 4, columns: 4, prefix: "B");
            AddSeats(context, room202.Id, rows: 2, columns: 2, prefix: "P");

            var room301 = new Room { FloorId = floor3.Id, Name = "301: VIP-зона", Type = "open_space", Capacity = 8 };
            var room302 = new Room { FloorId = floor3.Id, Name = "302: Кухня-лаунж", Type = "kitchen", Capacity = 10 };
            context.Rooms.AddRange(room301, room302);
            context.SaveChanges();

            AddSeats(context, room301.Id, rows: 2, columns: 4, prefix: "V");
            AddSeats(context, room302.Id, rows: 2, columns: 5, prefix: "K");

            context.SaveChanges();
            StatusMessage = "✓ Тестовые данные созданы: 3 этажа, 7 комнат, много мест";
            LoadFloors();
        }

        private void AddSeats(AppDbContext context, int roomId, int rows, int columns, string prefix)
        {
            for (int r = 1; r <= rows; r++)
            {
                for (int c = 1; c <= columns; c++)
                {
                    context.Seats.Add(new Seat
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

    public enum SeatStatus { Free, MyBooking, Busy }

    public partial class SeatDisplay : ObservableObject
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Column { get; set; }
        public SeatStatus Status { get; set; } = SeatStatus.Free;

        private string _displayText = string.Empty;
        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        private string _tooltip = string.Empty;
        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public Brush StatusColor => Status switch
        {
            SeatStatus.Free => new SolidColorBrush(Color.FromRgb(229, 231, 235)),     // серый
            SeatStatus.MyBooking => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // синий
            SeatStatus.Busy => new SolidColorBrush(Color.FromRgb(239, 68, 68)),       // красный
            _ => new SolidColorBrush(Colors.Gray)
        };

        public Brush TextColor => Status == SeatStatus.Free
            ? new SolidColorBrush(Color.FromRgb(55, 65, 81))
            : new SolidColorBrush(Colors.White);
    }
}
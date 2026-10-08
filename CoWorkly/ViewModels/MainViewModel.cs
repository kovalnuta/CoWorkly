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

        [ObservableProperty] private string _currentUserName = "Гость";
        [ObservableProperty] private string _statusMessage = "Выберите этаж";
        [ObservableProperty] private string _breadcrumb = "🏢 Коворкинг";

        public int MaxRow { get; private set; } = 1;
        public int MaxColumn { get; private set; } = 1;

        public MainViewModel()
        {
            LoadCurrentUser();
            LoadFloors();
        }

        private void LoadCurrentUser()
        {
            var user = Session.CurrentUser;

            if (user == null)
            {
                CurrentUserName = "Гость";
                return;
            }

            CurrentUserName = $"👤 {user.Username} ({user.Role})";
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
            if (floor == null) return;

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
            if (room == null) return;

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
                .OrderBy(s => s.Row)
                .ThenBy(s => s.Column)
                .ToList();

            var now = DateTime.UtcNow;

            var activeBookings = context.Bookings
                .Where(b => b.Seat.RoomId == roomId
                            && b.StartTime <= now
                            && b.EndTime > now)
                .Include(b => b.User)
                .ToList();

            Seats.Clear();

            MaxRow = seats.Any() ? seats.Max(s => s.Row) : 1;
            MaxColumn = seats.Any() ? seats.Max(s => s.Column) : 1;

            var currentUserId = Session.CurrentUser?.Id ?? 0;

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
                    display.Tooltip = $"Место {seat.Number} — свободно";
                }
                else if (booking.UserId == currentUserId)
                {
                    display.Status = SeatStatus.MyBooking;
                    display.DisplayText = "✓";
                    display.Tooltip = $"Ваша бронь до {booking.EndTime.ToLocalTime():HH:mm}";
                }
                else
                {
                    display.Status = SeatStatus.Busy;
                    display.DisplayText = string.IsNullOrEmpty(booking.User?.Username)
                        ? "?"
                        : booking.User!.Username.Substring(0, 1).ToUpper();
                    display.Tooltip = $"Занято: {booking.User?.Username} до {booking.EndTime.ToLocalTime():HH:mm}";
                }

                Seats.Add(display);
            }

            OnPropertyChanged(nameof(MaxRow));
            OnPropertyChanged(nameof(MaxColumn));

            var freeCount = Seats.Count(s => s.Status == SeatStatus.Free);
            StatusMessage = $"Свободно мест: {freeCount} из {Seats.Count}";
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentLevel == 3 && SelectedFloor != null)
            {
                SelectFloor(SelectedFloor);
            }
            else if (CurrentLevel == 2)
            {
                LoadFloors();
            }
        }

        [RelayCommand]
        private void SelectSeat(SeatDisplay seat)
        {
            if (seat == null) return;

            var currentUserId = Session.CurrentUser?.Id ?? 0;
            if (currentUserId == 0) return;

            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            var activeBooking = context.Bookings
                .FirstOrDefault(b => b.SeatId == seat.SeatId
                                     && b.StartTime <= now
                                     && b.EndTime > now);

            if (activeBooking != null)
            {
                if (activeBooking.UserId == currentUserId)
                {
                    context.Bookings.Remove(activeBooking);
                    context.SaveChanges();
                    StatusMessage = $"✓ Бронь места {seat.SeatNumber} отменена";
                }
                else
                {
                    StatusMessage = $"⛔ Место {seat.SeatNumber} уже занято";
                    return;
                }
            }
            else
            {
                var newBooking = new Booking
                {
                    UserId = currentUserId,
                    SeatId = seat.SeatId,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddHours(2),
                    CreatedAt = DateTime.UtcNow
                };

                context.Bookings.Add(newBooking);
                context.SaveChanges();

                StatusMessage = $"✓ Место {seat.SeatNumber} забронировано до {newBooking.EndTime.ToLocalTime():HH:mm}";
            }

            if (SelectedRoom != null)
                LoadSeats(SelectedRoom.Id);
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
            SeatStatus.Free => new SolidColorBrush(Color.FromRgb(0xED, 0xEA, 0xE4)),
            SeatStatus.MyBooking => new SolidColorBrush(Color.FromRgb(0xA8, 0xC5, 0xDA)),
            SeatStatus.Busy => new SolidColorBrush(Color.FromRgb(0xE8, 0xB4, 0xB4)),
            _ => new SolidColorBrush(Colors.Gray)
        };

        public Brush TextColor => Status == SeatStatus.Free
            ? new SolidColorBrush(Color.FromRgb(0x6B, 0x70, 0x7B))
            : new SolidColorBrush(Colors.White);
    }
}
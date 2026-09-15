using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CoWorkly.Data;
using CoWorkly.Models;

namespace CoWorkly
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

 
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            LoginError.Text = string.Empty;

            var username = LoginUsernameBox.Text.Trim();
            var password = LoginPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginError.Text = "Введите имя пользователя и пароль.";
                return;
            }

            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                LoginError.Text = "Пользователь с таким именем не найден.";
                return;
            }

            if (user.Password != password)
            {
                LoginError.Text = "Неверный пароль.";
                return;
            }

            App.CurrentUserId = user.Id;

            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            RegError.Text = string.Empty;

            var username = RegUsernameBox.Text.Trim();
            var email = RegEmailBox.Text.Trim();
            var password = RegPasswordBox.Password;
            var confirm = RegConfirmBox.Password;

            // Валидация
            if (string.IsNullOrWhiteSpace(username))
            {
                RegError.Text = "Введите имя пользователя.";
                return;
            }

            if (username.Length < 3)
            {
                RegError.Text = "Имя пользователя должно быть не короче 3 символов.";
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@") || !email.Contains("."))
            {
                RegError.Text = "Введите корректный email.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 5)
            {
                RegError.Text = "Пароль должен быть не короче 5 символов.";
                return;
            }

            if (password != confirm)
            {
                RegError.Text = "Пароли не совпадают.";
                return;
            }

            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (context.Users.Any(u => u.Username == username))
            {
                RegError.Text = "Пользователь с таким именем уже существует.";
                return;
            }

            if (context.Users.Any(u => u.Email == email))
            {
                RegError.Text = "Пользователь с таким email уже существует.";
                return;
            }

            var newUser = new User
            {
                Username = username,
                Email = email,
                Password = password,
                Role = "Client",
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(newUser);
            context.SaveChanges();
            App.CurrentUserId = newUser.Id;

            MessageBox.Show(
                $"Добро пожаловать, {newUser.Username}!",
                "Регистрация успешна",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}
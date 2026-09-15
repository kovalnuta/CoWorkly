using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
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
            LoadUsers();
        }

        private void LoadUsers()
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = context.Users.ToList();
            UsersComboBox.ItemsSource = users;
        }

        private void LoginAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            TryLogin("Анна");
        }

        private void LoginAsClient_Click(object sender, RoutedEventArgs e)
        {
            TryLogin("Кирилл");
        }

        private void TryLogin(string username)
        {
            using var scope = App.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                ErrorMessage.Text = $"Пользователь '{username}' не найден. Сначала создайте тестовые данные.";
                return;
            }

            App.CurrentUserId = user.Id;
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}
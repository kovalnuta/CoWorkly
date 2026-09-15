using System.Windows;
using CoWorkly.ViewModels;

namespace CoWorkly
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentUserId = 0;
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
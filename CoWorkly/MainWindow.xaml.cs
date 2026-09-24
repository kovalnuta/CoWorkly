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
            Session.SignOut();

            var login = new LoginWindow();
            login.Show();
            this.Close();
        }
    }
}
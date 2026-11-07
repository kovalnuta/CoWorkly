using CoWorkly.Models;
using CoWorkly.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

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
            Close();
        }

        // ============ Chrome (как в LoginWindow) ============

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { ToggleMaximize(); return; }
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void ToggleMaximize()
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                Root.CornerRadius = new CornerRadius(0);
                Root.Clip = null;
                Root.Effect = null;
            }
            else
            {
                Root.CornerRadius = new CornerRadius(12);
                Root.Clip = new RectangleGeometry(new Rect(0, 0, 10000, 10000), 12, 12);
                Root.Effect = new DropShadowEffect
                {
                    Color = (Color)FindResource("ShadowColor"),   // цвет из Themes/Colors.xaml
                    BlurRadius = 42,
                    ShadowDepth = 0,
                    Opacity = 0.18
                };
            }
        }
    }
}
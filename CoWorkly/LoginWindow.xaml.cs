using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Extensions.DependencyInjection;
using CoWorkly.Services;

namespace CoWorkly
{
    public partial class LoginWindow : Window
    {
        private bool _isLoginTab = true;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateIndicator(false);

            // Плашка вкладок подстраивается под ширину при изменении размера окна
            SizeChanged += (_, _) => UpdateIndicator(false);
        }

        // ============ Chrome ============

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

        // ============ Segmented tabs ============

        private void LoginTab_Click(object sender, RoutedEventArgs e) => SelectTab(true, true);
        private void RegTab_Click(object sender, RoutedEventArgs e) => SelectTab(false, true);

        private void SelectTab(bool login, bool animate)
        {
            if (_isLoginTab == login && animate) return;
            _isLoginTab = login;

            LoginTabBtn.Tag = login ? "Active" : null;
            RegTabBtn.Tag = login ? null : "Active";

            UpdateIndicator(animate);

            if (login) SwitchContent(LoginPanel, RegPanel, animate);
            else SwitchContent(RegPanel, LoginPanel, animate);
        }

        private void UpdateIndicator(bool animate)
        {
            if (TabsHost.ActualWidth <= 0) return;

            double half = TabsHost.ActualWidth / 2.0;
            TabIndicator.Width = half;

            double targetX = _isLoginTab ? 0 : half;

            if (!animate)
            {
                IndicatorTranslate.X = targetX;
                return;
            }

            var anim = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            IndicatorTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        //  Crossfade между панелями 

        private static void SwitchContent(FrameworkElement show, FrameworkElement hide, bool animate)
        {
            if (!animate)
            {
                show.Visibility = Visibility.Visible;
                hide.Visibility = Visibility.Collapsed;
                return;
            }

            if (hide.Visibility == Visibility.Visible)
            {
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
                fadeOut.Completed += (_, _) => hide.Visibility = Visibility.Collapsed;
                hide.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            show.Visibility = Visibility.Visible;
            show.Opacity = 0;

            if (show.RenderTransform is not TranslateTransform)
                show.RenderTransform = new TranslateTransform();

            var tr = (TranslateTransform)show.RenderTransform;
            tr.Y = 8;

            var dur = TimeSpan.FromMilliseconds(240);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            show.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, dur) { EasingFunction = ease });
            tr.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, dur) { EasingFunction = ease });
        }

        //  Вход 

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            ClearLoginErrors();

            using var scope = App.ServiceProvider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

            var result = auth.Login(LoginUsernameBox.Text, LoginPasswordBox.Password);

            if (!result.Success)
            {
                ShowError(LoginUsernameError, result.UsernameError);
                ShowError(LoginPasswordError, result.PasswordError);
                if (!string.IsNullOrEmpty(result.GeneralError))
                    LoginError.Text = result.GeneralError;
                return;
            }

            Session.SignIn(result.User!);
            OpenMainWindow();
        }

        // ============ Регистрация ============

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            ClearRegisterErrors();

            using var scope = App.ServiceProvider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

            var result = auth.Register(
                RegUsernameBox.Text, RegEmailBox.Text,
                RegPasswordBox.Password, RegConfirmBox.Password);

            if (!result.Success)
            {
                ShowError(RegUsernameError, result.UsernameError);
                ShowError(RegEmailError, result.EmailError);
                ShowError(RegPasswordError, result.PasswordError);
                ShowError(RegConfirmError, result.ConfirmError);
                if (!string.IsNullOrEmpty(result.GeneralError))
                    RegError.Text = result.GeneralError;
                return;
            }

            Session.SignIn(result.User!);
            // Окно сообщения в стиле HandyControl
            HandyControl.Controls.MessageBox.Success(
                $"Добро пожаловать, {result.User!.Username}!",
                "Аккаунт создан");
            OpenMainWindow();
        }



        private static void ShowError(TextBlock block, string? message)
        {
            block.Text = message ?? string.Empty;
            block.Visibility = string.IsNullOrEmpty(message)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearLoginErrors()
        {
            LoginError.Text = string.Empty;
            ShowError(LoginUsernameError, null);
            ShowError(LoginPasswordError, null);
        }

        private void ClearRegisterErrors()
        {
            RegError.Text = string.Empty;
            ShowError(RegUsernameError, null);
            ShowError(RegEmailError, null);
            ShowError(RegPasswordError, null);
            ShowError(RegConfirmError, null);
        }

        private void OpenMainWindow()
        {
            new MainWindow().Show();
            Close();
        }
    }
}

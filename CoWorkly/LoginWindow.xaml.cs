using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CoWorkly.Services;

namespace CoWorkly
{
    public partial class LoginWindow : Window
    {
        // Коды иконок шрифта Segoe MDL2 Assets
        private const string EyeIconShow = "\uE7B3"; // открытый глаз — показать пароль
        private const string EyeIconHide = "\uED1A"; // перечёркнутый глаз — скрыть пароль

        public LoginWindow()
        {
            InitializeComponent();
        }

        // ================ ВХОД ================

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            ClearLoginErrors();

            using var scope = App.ServiceProvider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

            string username = LoginUsernameBox.Text;
            string password = GetPassword(LoginPasswordBox, LoginPasswordVisible);

            var result = auth.Login(username, password);

            if (!result.Success)
            {
                if (!string.IsNullOrEmpty(result.GeneralError))
                    LoginError.Text = result.GeneralError;

                return;
            }

            Session.SignIn(result.User!);
            OpenMainWindow();
        }

        // ================ РЕГИСТРАЦИЯ ================

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            ClearRegisterErrors();

            using var scope = App.ServiceProvider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

            string username = RegUsernameBox.Text;
            string email = RegEmailBox.Text;
            string password = GetPassword(RegPasswordBox, RegPasswordVisible);
            string confirm = GetPassword(RegConfirmBox, RegConfirmVisible);

            var result = auth.Register(username, email, password, confirm);

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

            MessageBox.Show(
                $"Добро пожаловать, {result.User!.Username}!",
                "Регистрация успешна",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            OpenMainWindow();
        }

        private void ToggleLoginPassword_Click(object sender, RoutedEventArgs e)
            => TogglePassword(LoginPasswordBox, LoginPasswordVisible, LoginPasswordToggle);

        private void ToggleRegPassword_Click(object sender, RoutedEventArgs e)
            => TogglePassword(RegPasswordBox, RegPasswordVisible, RegPasswordToggle);

        private void ToggleRegConfirm_Click(object sender, RoutedEventArgs e)
            => TogglePassword(RegConfirmBox, RegConfirmVisible, RegConfirmToggle);

        /// <summary>
        /// Переключает между скрытым PasswordBox и видимым TextBox.
        /// Синхронизирует текст в обоих направлениях и меняет иконку на кнопке.
        /// </summary>
        private void TogglePassword(PasswordBox pwd, TextBox txt, Button toggle)
        {
            if (pwd.Visibility == Visibility.Visible)
            {
            
                txt.Text = pwd.Password;
                pwd.Visibility = Visibility.Collapsed;
                txt.Visibility = Visibility.Visible;
                txt.Focus();
                txt.CaretIndex = txt.Text.Length;
                toggle.Content = EyeIconHide;
                toggle.ToolTip = "Скрыть пароль";
            }
            else
            {
                // Переходим в режим "скрыть пароль"
                pwd.Password = txt.Text;
                txt.Visibility = Visibility.Collapsed;
                pwd.Visibility = Visibility.Visible;
                pwd.Focus();
                toggle.Content = EyeIconShow;
                toggle.ToolTip = "Показать пароль";
            }
        }

        /// <summary>
        /// Возвращает пароль из видимого в данный момент элемента — PasswordBox или TextBox.
        /// </summary>
        private string GetPassword(PasswordBox pwd, TextBox txt)
        {
            return pwd.Visibility == Visibility.Visible
                ? pwd.Password
                : txt.Text;
        }

        // ================ ВСПОМОГАТЕЛЬНОЕ ================

        private void ShowError(TextBlock block, string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                block.Text = string.Empty;
                block.Visibility = Visibility.Collapsed;
            }
            else
            {
                block.Text = message;
                block.Visibility = Visibility.Visible;
            }
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
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}
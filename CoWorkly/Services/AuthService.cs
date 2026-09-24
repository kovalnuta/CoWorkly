using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CoWorkly.Data;
using CoWorkly.Models;

namespace CoWorkly.Services
{
    public class AuthResult
    {
        public bool Success { get; init; }
        public User? User { get; init; }

        public string? UsernameError { get; init; }
        public string? EmailError { get; init; }
        public string? PasswordError { get; init; }
        public string? ConfirmError { get; init; }
        public string? GeneralError { get; init; }

        public static AuthResult Ok(User user) => new() { Success = true, User = user };
    }

    public class AuthService
    {
        private readonly AppDbContext _context;

        // Разрешённые домены для email
        private static readonly HashSet<string> AllowedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com",
            "mail.ru",
            "yandex.ru",
            "ya.ru",
            "bk.ru",
            "inbox.ru",
            "list.ru",
            "vk.com",
            "rambler.ru",
            "outlook.com",
            "hotmail.com",
            "icloud.com"
        };

        // Только латиница, цифры и подчёркивание
        private static readonly Regex UsernameRegex = new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        // Спецсимволы для пароля
        private static readonly Regex SpecialCharRegex = new(@"[!@#$%^&*()_\-+=\[\]{};:'""\\|,.<>/?~`]", RegexOptions.Compiled);

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        // ---------------- ВХОД ----------------

        public AuthResult Login(string username, string password)
        {
            username = (username ?? string.Empty).Trim();
            password ??= string.Empty;

            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
                return new AuthResult { Success = false, GeneralError = "Введите имя пользователя и пароль." };

            if (string.IsNullOrWhiteSpace(username))
                return new AuthResult { Success = false, GeneralError = "Введите имя пользователя." };

            if (string.IsNullOrWhiteSpace(password))
                return new AuthResult { Success = false, GeneralError = "Введите пароль." };

            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return new AuthResult { Success = false, GeneralError = "Пользователь с таким именем не найден." };

            if (!PasswordHasher.Verify(password, user.PasswordHash))
                return new AuthResult { Success = false, GeneralError = "Неверный пароль." };

            return AuthResult.Ok(user);
        }

        // ---------------- РЕГИСТРАЦИЯ ----------------

        public AuthResult Register(string username, string email, string password, string confirm)
        {
            username = (username ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            password ??= string.Empty;
            confirm ??= string.Empty;

            string? usernameError = ValidateUsernameFormat(username);
            string? emailError = ValidateEmailFormat(email);
            string? passwordError = ValidatePasswordFormat(password);

            string? confirmError = null;
            if (string.IsNullOrEmpty(confirm))
                confirmError = "Повторите пароль.";
            else if (password != confirm)
                confirmError = "Пароли не совпадают.";

            bool hasFormatErrors =
                usernameError != null || emailError != null ||
                passwordError != null || confirmError != null;

            if (hasFormatErrors)
            {
                return new AuthResult
                {
                    Success = false,
                    UsernameError = usernameError,
                    EmailError = emailError,
                    PasswordError = passwordError,
                    ConfirmError = confirmError
                };
            }

            // Проверка уникальности — только если формат верный
            if (_context.Users.Any(u => u.Username == username))
                usernameError = "Пользователь с таким именем уже существует.";

            if (_context.Users.Any(u => u.Email == email))
                emailError = "Пользователь с таким email уже существует.";

            if (usernameError != null || emailError != null)
            {
                return new AuthResult
                {
                    Success = false,
                    UsernameError = usernameError,
                    EmailError = emailError
                };
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = PasswordHasher.Hash(password),
                Role = "Client",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return AuthResult.Ok(user);
        }

        // ---------------- ВАЛИДАЦИЯ ----------------

        private static string? ValidateUsernameFormat(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "Введите имя пользователя.";

            if (username.Length < 3)
                return "Имя пользователя должно быть не короче 3 символов.";

            if (username.Length > 50)
                return "Имя пользователя слишком длинное (максимум 50 символов).";

            if (!UsernameRegex.IsMatch(username))
                return "Имя пользователя может содержать только латинские буквы, цифры и подчёркивание.";

            return null;
        }

        private static string? ValidateEmailFormat(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "Введите email.";

            if (email.IndexOf(' ') >= 0)
                return "Email не должен содержать пробелов.";

            int at = email.IndexOf('@');

            if (at < 0)
                return "Некорректный email: отсутствует символ @.";

            if (at == 0)
                return "Некорректный email: укажите имя перед @.";

            if (at == email.Length - 1)
                return "Некорректный email: укажите домен после @.";

            string domain = email.Substring(at + 1);

            if (!domain.Contains('.'))
                return "Некорректный email: проверьте домен.";

            if (!AllowedDomains.Contains(domain))
                return "Поддерживаются только почты: gmail.com, mail.ru, yandex.ru, ya.ru, bk.ru, inbox.ru, list.ru, vk.com, rambler.ru, outlook.com, hotmail.com, icloud.com.";

            return null;
        }

        private static string? ValidatePasswordFormat(string password)
        {
            if (string.IsNullOrEmpty(password))
                return "Введите пароль.";

            if (password.Length < 8)
                return "Пароль должен быть не короче 8 символов.";

            if (!password.Any(char.IsLower))
                return "Пароль должен содержать хотя бы одну строчную букву.";

            if (!password.Any(char.IsUpper))
                return "Пароль должен содержать хотя бы одну заглавную букву.";

            if (!password.Any(char.IsDigit))
                return "Пароль должен содержать хотя бы одну цифру.";

            if (!SpecialCharRegex.IsMatch(password))
                return "Пароль должен содержать хотя бы один специальный символ (!@#$% и т.п.).";

            return null;
        }
    }
}
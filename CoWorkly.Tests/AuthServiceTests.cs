using System;
using CoWorkly.Data;
using CoWorkly.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoWorkly.Tests
{
    public class AuthServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly AuthService _auth;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _auth = new AuthService(_context);
        }

        public void Dispose() => _context.Dispose();

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("аБв")]
        [InlineData("user name")]
        [InlineData("user@name")]
        [InlineData("user-name")]
        public void BadUsername(string username)
        {
            var result = _auth.Register(username, "user@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.False(result.Success);
            Assert.NotNull(result.UsernameError);
        }

        [Fact]
        public void DuplicateUsername()
        {
            _auth.Register("testuser", "one@gmail.com", "Passw0rd!", "Passw0rd!");
            var result = _auth.Register("testuser", "two@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.False(result.Success);
            Assert.Contains("уже существует", result.UsernameError);
        }

        [Fact]
        public void MinUsername()
        {
            var result = _auth.Register("abc", "abc@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.True(result.Success);
        }

        [Theory]
        [InlineData("")]
        [InlineData("no-at-sign.com")]
        [InlineData("@mail.ru")]
        [InlineData("user@")]
        [InlineData("user@nomail")]
        [InlineData("user@unknown.com")]
        [InlineData("user @mail.ru")]
        public void BadEmail(string email)
        {
            var result = _auth.Register("validuser", email, "Passw0rd!", "Passw0rd!");

            Assert.False(result.Success);
            Assert.NotNull(result.EmailError);
        }

        [Theory]
        [InlineData("user@gmail.com")]
        [InlineData("user@mail.ru")]
        [InlineData("user@yandex.ru")]
        [InlineData("user@bk.ru")]
        [InlineData("user@inbox.ru")]
        public void GoodEmail(string email)
        {
            var result = _auth.Register("newuser", email, "Passw0rd!", "Passw0rd!");

            Assert.True(result.Success);
        }

        [Fact]
        public void DuplicateEmail()
        {
            _auth.Register("user1", "same@gmail.com", "Passw0rd!", "Passw0rd!");
            var result = _auth.Register("user2", "same@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.False(result.Success);
            Assert.Contains("уже существует", result.EmailError);
        }

        [Theory]
        [InlineData("")]
        [InlineData("short1!")]
        [InlineData("alllowercase1!")]
        [InlineData("ALLUPPERCASE1!")]
        [InlineData("NoDigits!!")]
        [InlineData("NoSpecial1")]
        [InlineData("12345678")]
        public void BadPassword(string password)
        {
            var result = _auth.Register("validuser", "user@gmail.com", password, password);

            Assert.False(result.Success);
            Assert.NotNull(result.PasswordError);
        }

        [Fact]
        public void NoSpecial()
        {
            var result = _auth.Register("newuser", "new@gmail.com", "Passw0rd", "Passw0rd");

            Assert.False(result.Success);
        }

        [Fact]
        public void ConfirmMismatch()
        {
            var result = _auth.Register("validuser", "user@gmail.com", "Passw0rd!", "Different1!");

            Assert.False(result.Success);
            Assert.NotNull(result.ConfirmError);
        }

        [Fact]
        public void RegisterOk()
        {
            var result = _auth.Register("newuser", "new@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.True(result.Success);
            Assert.NotNull(result.User);
            Assert.Equal("newuser", result.User!.Username);
            Assert.Equal("Client", result.User.Role);
            Assert.NotEqual("Passw0rd!", result.User.PasswordHash);
        }

        [Fact]
        public void RegisterHashes()
        {
            var result = _auth.Register("newuser", "new@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.StartsWith("$2", result.User!.PasswordHash);
        }

        [Fact]
        public void LoginOk()
        {
            _auth.Register("validuser", "user@gmail.com", "Passw0rd!", "Passw0rd!");
            var result = _auth.Login("validuser", "Passw0rd!");

            Assert.True(result.Success);
            Assert.NotNull(result.User);
        }

        [Fact]
        public void LoginWrongPass()
        {
            _auth.Register("validuser", "user@gmail.com", "Passw0rd!", "Passw0rd!");
            var result = _auth.Login("validuser", "WrongPass1!");

            Assert.False(result.Success);
            Assert.Contains("Неверный пароль", result.GeneralError);
        }

        [Fact]
        public void LoginUnknown()
        {
            var result = _auth.Login("unknownuser", "Passw0rd!");

            Assert.False(result.Success);
            Assert.Contains("не найден", result.GeneralError);
        }

        [Fact]
        public void LoginEmpty()
        {
            var result = _auth.Login("", "");

            Assert.False(result.Success);
            Assert.NotNull(result.GeneralError);
        }

        [Fact]
        public void LoginNoPass()
        {
            var result = _auth.Login("validuser", "");

            Assert.False(result.Success);
            Assert.NotNull(result.GeneralError);
        }

        [Fact]
        public void LoginCaseSensitive()
        {
            _auth.Register("validuser", "user@gmail.com", "Passw0rd!", "Passw0rd!");
            var result = _auth.Login("ValidUser", "Passw0rd!");

            Assert.False(result.Success);
        }
    }
}
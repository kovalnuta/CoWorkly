using System;
using System.Linq;
using CoWorkly.Data;
using CoWorkly.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoWorkly.Tests
{
    public class PasswordValidationTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly AuthService _auth;

        public PasswordValidationTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _auth = new AuthService(_context);
        }

        public void Dispose() => _context.Dispose();

        [Theory]
        [InlineData("Aa1!aaaa")]
        [InlineData("Aa1!bbbb")]
        [InlineData("Xy9#zzzz")]
        [InlineData("Passw0rd!")]
        [InlineData("Qwerty12#")]
        public void GoodPassword(string password)
        {
            var username = "user" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var email = username + "@gmail.com";
            var result = _auth.Register(username, email, password, password);

            Assert.True(result.Success);
        }

        [Fact]
        public void HashStored()
        {
            var result = _auth.Register("newuser", "new@gmail.com", "Passw0rd!", "Passw0rd!");

            Assert.NotNull(result.User);
            Assert.NotEqual("Passw0rd!", result.User!.PasswordHash);
            Assert.StartsWith("$2", result.User.PasswordHash);
        }

        [Fact]
        public void ManyUsers()
        {
            for (int i = 0; i < 5; i++)
            {
                var result = _auth.Register("user" + i, "user" + i + "@gmail.com", "Passw0rd!", "Passw0rd!");
                Assert.True(result.Success);
            }

            Assert.Equal(5, _context.Users.Count());
        }
    }
}
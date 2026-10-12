using CoWorkly.Models;
using CoWorkly.Services;
using Xunit;

namespace CoWorkly.Tests
{
    public class AuthResultTests
    {
        [Fact]
        public void OkResult()
        {
            var user = new User { Id = 1, Username = "test" };
            var result = AuthResult.Ok(user);

            Assert.True(result.Success);
            Assert.Equal(user, result.User);
            Assert.Null(result.UsernameError);
            Assert.Null(result.EmailError);
        }

        [Fact]
        public void FailResult()
        {
            var result = new AuthResult
            {
                Success = false,
                UsernameError = "Ошибка"
            };

            Assert.False(result.Success);
            Assert.NotNull(result.UsernameError);
            Assert.Null(result.User);
        }
    }
}
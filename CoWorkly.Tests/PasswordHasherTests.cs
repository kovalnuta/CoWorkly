using CoWorkly.Services;
using Xunit;

namespace CoWorkly.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void HashDiffers()
        {
            var hash = PasswordHasher.Hash("Passw0rd!");

            Assert.NotEqual("Passw0rd!", hash);
            Assert.StartsWith("$2", hash);
        }

        [Fact]
        public void HashUnique()
        {
            var hash1 = PasswordHasher.Hash("Passw0rd!");
            var hash2 = PasswordHasher.Hash("Passw0rd!");

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashHides()
        {
            var hash = PasswordHasher.Hash("SecretPassw0rd!");

            Assert.DoesNotContain("SecretPassw0rd!", hash);
        }

        [Fact]
        public void VerifyOk()
        {
            var hash = PasswordHasher.Hash("Passw0rd!");

            Assert.True(PasswordHasher.Verify("Passw0rd!", hash));
        }

        [Fact]
        public void VerifyWrong()
        {
            var hash = PasswordHasher.Hash("Passw0rd!");

            Assert.False(PasswordHasher.Verify("WrongPass1!", hash));
        }

        [Fact]
        public void VerifyEmptyPassword()
        {
            var hash = PasswordHasher.Hash("Passw0rd!");

            Assert.False(PasswordHasher.Verify("", hash));
        }

        [Fact]
        public void VerifyEmptyHash()
        {
            Assert.False(PasswordHasher.Verify("Passw0rd!", ""));
        }

        [Fact]
        public void VerifyCaseSensitive()
        {
            var hash = PasswordHasher.Hash("Passw0rd!");

            Assert.False(PasswordHasher.Verify("passw0rd!", hash));
        }
    }
}
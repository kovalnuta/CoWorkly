using CoWorkly.Models;

namespace CoWorkly
{
    public static class Session
    {
        public static User? CurrentUser { get; private set; }

        public static bool IsAuthenticated => CurrentUser != null;

        public static bool IsAdmin => CurrentUser?.Role == "Admin";

        public static void SignIn(User user)
        {
            CurrentUser = user;
        }

        public static void SignOut()
        {
            CurrentUser = null;
        }
    }
}
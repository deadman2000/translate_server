namespace TranslateServer.Model
{
    public class User : Document
    {
        public string Login { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }

        public void SetPassword(string password)
        {
            Password = HashPassword(password);
        }

        public bool CheckPassword(string pwd)
        {
            return BCrypt.Net.BCrypt.Verify(pwd, Password);
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}

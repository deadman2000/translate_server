namespace TranslateServer.Model
{
    public class User : Document
    {
        public string Login { get; set; }

        public string Password { get; set; }

        public string Role { get; set; }

        public void SetPassword(string pwd)
        {
            Password = BCrypt.Net.BCrypt.HashPassword(pwd);
        }

        public bool CheckPassword(string pwd)
        {
            return BCrypt.Net.BCrypt.Verify(pwd, Password);
        }
    }
}

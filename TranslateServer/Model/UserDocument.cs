using System.Text.Json.Serialization;

namespace TranslateServer.Model
{
    public class UserDocument : Document
    {
        public string Login { get; set; }

        [JsonIgnore]
        public string Password { get; set; }

        public string Role { get; set; }

        public int Letters { get; set; }

        public void SetPassword(string pwd)
        {
            Password = BCrypt.Net.BCrypt.HashPassword(pwd);
        }

        public bool CheckPassword(string pwd)
        {
            return BCrypt.Net.BCrypt.Verify(pwd, Password);
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public const string ADMIN = "Admin";
        public const string EDITOR = "Editor";
    }
}

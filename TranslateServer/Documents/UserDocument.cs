using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TranslateServer.Model;

namespace TranslateServer.Documents
{
    public class UserDocument : Document
    {
        public string Login { get; set; }

        [JsonIgnore]
        public string Password { get; set; }

        public string Role { get; set; }

        [BsonIgnore]
        public IEnumerable<ProjectLetters> LettersByProject { get; set; }

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
        public const string SHARED = "Shared";
    }
}

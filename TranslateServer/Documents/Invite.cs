using System;

namespace TranslateServer.Documents
{
    public class Invite : Document
    {
        public string Code { get; set; }

        public string Role { get; set; }

        public string UserCreated { get; set; }

        public DateTime DateCreate { get; set; }

        public bool Activated { get; set; }

        public DateTime? DateActivate { get; set; }

        public string UserActivated { get; set; }
    }
}

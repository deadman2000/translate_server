using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using TranslateServer.Model;

namespace TranslateServer.Services
{
    public class RunnersService
    {
        private readonly Dictionary<string, Runner> _runners = new();

        public void RegisterActivity(string runnerId, HttpRequest request)
        {
            var ip = request.Headers["X-Real-Ip"].FirstOrDefault() ?? request.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!_runners.TryGetValue(runnerId, out var runner))
            {
                _runners[runnerId] = new Runner
                {
                    Id = runnerId,
                    Ip = ip,
                    LastActivity = DateTime.UtcNow
                };
            }
            else
            {
                runner.Ip = ip;
                runner.LastActivity = DateTime.UtcNow;
            }
        }

        public IEnumerable<Runner> List() => _runners.Values;
    }
}

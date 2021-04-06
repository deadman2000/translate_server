using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TranslateServer.Mongo;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        public AuthController(MongoService mongo)
        {
        }

        public class AuthRequest
        {
            public string Username { get; set; }

            public string Password { get; set; }

            public bool Persistent { get; set; }
        }

        [HttpPost]
        public async Task Auth([FromBody] AuthRequest request)
        {
            /*var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, user.Id.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.Code));

            foreach (var property in typeof(UserRole).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name.StartsWith("Access") && property.GetValue(user.Role) is bool access && access)
                    identity.AddClaim(new Claim("Permission", property.Name));
            }

            var principal = new ClaimsPrincipal(identity);

            var props = new AuthenticationProperties();
            props.IsPersistent = request.Persistent;

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);*/
        }
    }
}

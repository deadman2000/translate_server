using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ApiController
    {
        private readonly UsersService _users;

        public UsersController(UsersService users)
        {
            _users = users;
        }

        // http://localhost:5000/api/users/init
        [HttpGet("init")]
        public async Task<ActionResult> Init()
        {
            var cnt = await _users.Collection.CountDocumentsAsync(u => true);
            if (cnt > 0)
                return ApiBadRequest("Database is not empty");

            var user = new UserDocument
            {
                Login = "admin",
                Role = UserDocument.ADMIN
            };
            user.SetPassword("admin");
            await _users.Insert(user);

            return Ok("success");
        }

        [HttpGet("me")]
        [Authorize]
        public ActionResult Me()
        {
            var role = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).FirstOrDefault();

            return Ok(new
            {
                Login = User.Identity.Name,
                Role = role
            });
        }

        public class LoginRequest
        {
            public string Login { get; set; }

            public string Password { get; set; }
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _users.Get(u => u.Login == request.Login);
            if (user == null || !user.CheckPassword(request.Password))
                return ApiBadRequest("Wrong login or password");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                //AllowRefresh = <bool>,
                // Refreshing the authentication session should be allowed.

                //ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
                // The time at which the authentication ticket expires. A 
                // value set here overrides the ExpireTimeSpan option of 
                // CookieAuthenticationOptions set with AddCookie.

                IsPersistent = true,
                // Whether the authentication session is persisted across 
                // multiple requests. When used with cookies, controls
                // whether the cookie's lifetime is absolute (matching the
                // lifetime of the authentication ticket) or session-based.

                //IssuedUtc = <DateTimeOffset>,
                // The time at which the authentication ticket was issued.
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return Ok();
        }

        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }
    }
}

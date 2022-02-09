using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
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
                Login = UserLogin,
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

            await Authorize(user);

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

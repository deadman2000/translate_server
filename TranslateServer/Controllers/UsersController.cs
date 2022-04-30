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

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult> Me([FromServices] TranslateService translate)
        {
            var user = await _users.Get(u => u.Login == UserLogin);
            if (user == null)
                return Forbid();

            var letters = await translate.GetUserLetters(UserLogin);

            return Ok(new
            {
                user.Login,
                user.Role,
                letters,
            });
        }

        public class ChangePasswordRequest
        {
            public string Password { get; set; }
        }

        [Authorize]
        [HttpPost("changepassword")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            await _users.Update()
                .Where(u => u.Login == UserLogin)
                .Set(u => u.Password, Model.User.HashPassword(request.Password))
                .Execute();
            return Ok();
        }

        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        #region Administration

        [AuthAdmin]
        [HttpGet]
        public async Task<ActionResult> GetList([FromServices] TranslateService translate)
        {
            var users = (await _users.All()).ToList();
            foreach (var user in users)
                user.Letters = await translate.GetUserLetters(user.Login);
            
            return Ok(users);
        }

        [AuthAdmin]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _users.DeleteOne(u => u.Id == id);
            return Ok();
        }

        public class SetPasswordRequest
        {
            public string UserId { get; set; }

            public string Password { get; set; }
        }

        [AuthAdmin]
        [HttpPost("setpassword")]
        public async Task<ActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            await _users.Update()
                .Where(u => u.Id == request.UserId)
                .Set(u => u.Password, Model.User.HashPassword(request.Password))
                .Execute();
            return Ok();
        }

        #endregion
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ApiController
    {
        private readonly UsersStore _users;
        private readonly CommentNotifyStore _commentNotify;

        public UsersController(UsersStore users, CommentNotifyStore commentNotify)
        {
            _users = users;
            _commentNotify = commentNotify;
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
        public async Task<ActionResult> Me([FromServices] TranslateStore translate)
        {
            var user = await _users.Get(u => u.Login == UserLogin);
            if (user == null)
                return Forbid();

            var letters = await translate.GetUserLetters(UserLogin);
            var unread = await _commentNotify.Queryable()
                .Where(n => n.User == UserLogin && !n.Read)
                .CountAsync();

            return Ok(new
            {
                user.Login,
                user.Role,
                letters,
                unread,
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
                .Set(u => u.Password, UserDocument.HashPassword(request.Password))
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
        public async Task<ActionResult> GetList([FromServices] TranslateStore translate)
        {
            var users = (await _users.All()).ToList();
            foreach (var user in users)
                user.LettersByProject = await translate.GetUserLetters(user.Login);
            
            return Ok(users);
        }

        [AuthAdmin]
        [HttpGet("{id}/chart")]
        public async Task<ActionResult> GetChart(string id, [FromServices] TranslateStore translate)
        {
            var user = await _users.GetById(id);
            if (user == null) return NotFound();
            var chart = await translate.GetChart(user.Login);
            return Ok(chart);
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
                .Set(u => u.Password, UserDocument.HashPassword(request.Password))
                .Execute();
            return Ok();
        }

        #endregion
    }
}

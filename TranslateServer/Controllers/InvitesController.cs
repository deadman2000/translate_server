using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitesController : ApiController
    {
        private readonly InvitesStore _invites;
        private readonly UsersStore _users;

        public InvitesController(InvitesStore invites, UsersStore users)
        {
            _invites = invites;
            _users = users;
        }

        [AuthAdmin]
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var invites = await _invites.Queryable()
                .Where(i => true)
                .OrderByDescending(i => i.DateCreate)
                .ToListAsync();
            return Ok(invites);
        }

        public class CreateInviteRequest
        {
            public string Role { get; set; }
        }

        [AuthAdmin]
        [HttpPost]
        public async Task<ActionResult> Create(CreateInviteRequest request)
        {
            var invite = new Invite
            {
                Code = Guid.NewGuid().ToString(),
                Role = request.Role,
                DateCreate = DateTime.UtcNow,
                UserCreated = UserLogin
            };
            await _invites.Insert(invite);
            return Ok(invite);
        }

        [AuthAdmin]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _invites.DeleteOne(i => i.Id == id);
            return Ok();
        }

        [HttpGet("valid/{code}")]
        public async Task<ActionResult> IsValid(string code)
        {
            var invite = await _invites.Get(i => i.Code == code);
            if (invite == null) return NotFound();

            return Ok(!invite.Activated);
        }

        public class ActivateRequest
        {
            public string Code { get; set; }

            public string Login { get; set; }

            public string Password { get; set; }
        }

        [HttpPost("activate")]
        public async Task<ActionResult> Activate([FromBody] ActivateRequest request)
        {
            var invite = await _invites.Get(i => i.Code == request.Code);
            if (invite == null) return NotFound();
            if (invite.Activated) return ApiBadRequest("Invite already activated");

            var user = await _users.Get(u => u.Login == request.Login);
            if (user != null) return ApiBadRequest("User with this login is already registered");

            user = new UserDocument
            {
                Login = request.Login,
                Role = invite.Role,
            };
            user.SetPassword(request.Password);
            await _users.Insert(user);

            await _invites.Update()
                .Where(i => i.Id == invite.Id)
                .Set(i => i.Activated, true)
                .Set(i => i.DateActivate, DateTime.UtcNow)
                .Set(i => i.UserActivated, user.Login)
                .Execute();

            await Authorize(user);

            return Ok();
        }
    }
}

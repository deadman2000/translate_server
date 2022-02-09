﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InvitesController : ApiController
    {
        private readonly InvitesService _invites;
        private readonly UsersService _users;

        public InvitesController(InvitesService invites, UsersService users)
        {
            _invites = invites;
            _users = users;
        }

        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var invites = await _invites.All();
            return Ok(invites);
        }

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            var invite = new Invite
            {
                Code = Guid.NewGuid().ToString(),
                DateCreate = DateTime.UtcNow,
                UserCreated = UserLogin
            };
            await _invites.Insert(invite);
            return Ok(invite);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _invites.Delete(i => i.Id == id);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("valid/{code}")]
        public async Task<ActionResult> IsActive(string code)
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

        [AllowAnonymous]
        [HttpPost("activate")]
        public async Task<ActionResult> Activate([FromForm] ActivateRequest request)
        {
            var invite = await _invites.Get(i => i.Code == request.Code);
            if (invite == null) return NotFound();
            if (invite.Activated) return ApiBadRequest("Invite already activated");

            var user = await _users.Get(u => u.Login == request.Login);
            if (user != null) return BadRequest("User with this login is already registered");

            user = new UserDocument
            {
                Login = request.Login,
                Role = UserDocument.EDITOR
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

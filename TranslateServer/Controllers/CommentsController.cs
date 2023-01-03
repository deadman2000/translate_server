using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ApiController
    {
        private readonly CommentsStore _comments;
        private readonly TranslateStore _translate;
        private readonly UsersStore _users;
        private readonly CommentNotifyStore _commentNotify;

        public CommentsController(CommentsStore service, TranslateStore translate, UsersStore users, CommentNotifyStore commentNotify)
        {
            _comments = service;
            _translate = translate;
            _users = users;
            _commentNotify = commentNotify;
        }

        public class SubmitRequest
        {
            public string TranslateId { get; set; }

            public string Text { get; set; }
        }

        [HttpPost("submit")]
        public async Task<ActionResult> Submit([FromBody] SubmitRequest request)
        {
            var tr = await _translate.GetById(request.TranslateId);
            if (tr == null) return NotFound();

            var comment = new Comment
            {
                TranslateId = tr.FirstId ?? tr.Id,
                Project = tr.Project,
                Volume = tr.Volume,
                Number = tr.Number,
                Text = request.Text,
                Author = UserLogin,
                DateCreate = DateTime.UtcNow,
            };

            await _comments.Insert(comment);

            // Отправляем уведомление всем админам, автору перевода и участникам обсуждения
            List<string> dest = new();

            var commentors = await _comments.Queryable() // Комментаторы
                .Where(c => c.TranslateId == tr.Id && c.Author != UserLogin)
                .Select(c => c.Author)
                .ToListAsync();
            dest.AddRange(commentors);

            var admins = await _users.Queryable() // Админы
                .Where(u => u.Role == UserDocument.ADMIN && u.Login != UserLogin)
                .Select(u => u.Login)
                .ToListAsync();
            dest.AddRange(admins);

            if (tr.Author != UserLogin)
                dest.Add(tr.Author);

            foreach (var user in dest.Distinct())
            {
                await _commentNotify.Insert(new CommentNotify
                {
                    CommentId = comment.Id,
                    Date = comment.DateCreate,
                    User = user
                });
                // TODO recalc user's unread comments
            }

            return Ok(comment);
        }

        [HttpGet("translate/{translateId}")]
        public async Task<ActionResult<IEnumerable<Comment>>> GetByTranslate(string translateId)
        {
            return Ok(await _comments.Queryable()
                .Where(t => t.TranslateId == translateId)
                .OrderBy(t => t.DateCreate)
                .ToListAsync()
            );
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var comment = await _comments.GetById(id);
            if (comment == null)
                return BadRequest();
            if (!IsAdmin && comment.Author != UserLogin)
                return Forbid();

            await _comments.DeleteOne(c => c.Id == id);
            var notifyResult = await _commentNotify.Delete(n => n.CommentId == id);

            return Ok();
        }
    }
}

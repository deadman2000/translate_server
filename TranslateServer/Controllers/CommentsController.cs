using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ApiController
    {
        private readonly CommentsService _comments;
        private readonly TranslateService _translate;

        public CommentsController(CommentsService service, TranslateService translate)
        {
            _comments = service;
            _translate = translate;
        }

        public class SubmitRequest
        {
            public string TranslateId { get; set; }

            public string Text { get; set; }
        }

        [HttpPost("submit")]
        public async Task<ActionResult> Submit([FromBody] SubmitRequest request)
        {
            var comment = new Comment
            {
                TranslateId = request.TranslateId,
                Text = request.Text,
                Author = UserLogin,
                DateCreate = DateTime.UtcNow,
            };

            await _comments.Insert(comment);

            await RecalcComments(request.TranslateId);

            return Ok(comment);
        }

        [HttpGet("translate/{translateId}")]
        public async Task<ActionResult<IEnumerable<Comment>>> GetByTranslate(string translateId)
        {
            return Ok(await _comments.Collection.AsQueryable()
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

            await _comments.DeleteOne(c => c.Id == id && (IsAdmin || c.Author == UserLogin));
            await RecalcComments(comment.TranslateId);

            return Ok();
        }

        private async Task RecalcComments(string translateId)
        {
            var count = await _comments.Collection.AsQueryable()
                .Where(c => c.TranslateId == translateId)
                .CountAsync();

            await _translate
                .Update(t => t.Id == translateId)
                .Set(t => t.Comments, count)
                .Execute();
        }
    }
}

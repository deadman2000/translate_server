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

        public CommentsController(CommentsService service)
        {
            _comments = service;
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
            var result = await _comments.Delete(c => c.Id == id && c.Author == UserLogin); // TODO Admin can delete everything
            if (result.DeletedCount == 0)
                return BadRequest();

            return Ok();
        }
    }
}

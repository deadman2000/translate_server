using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Requests;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TranslateController : ApiController
    {
        private readonly TranslateService _translateService;
        private readonly TranslateStore _translate;
        private readonly TextsStore _texts;
        private readonly SearchService _search;
        private readonly CommentsStore _comments;

        public TranslateController(TranslateService translateService,
            TranslateStore translate,
            TextsStore texts,
            SearchService search,
            CommentsStore comments,
            ProjectsStore projects)
        {
            _translateService = translateService;
            _translate = translate;
            _texts = texts;
            _search = search;
            _comments = comments;
            _projects = projects;
        }

        public class SubmitRequest
        {
            public string Project { get; set; }

            public string Volume { get; set; }

            public int Number { get; set; }

            public string TranslateId { get; set; }

            public string Text { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Submit([FromBody] SubmitRequest request)
        {
            if (!await HasAccessToProject(request.Project)) return NotFound();

            var translate = await _translateService.Submit(request.Project, request.Volume, request.Number, request.Text, UserLogin, false, request.TranslateId);
            if (translate == null) return NotFound();

            var comments = await _comments.GetComments(translate);

            return Ok(new TranslateInfo(translate, comments));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var tr = await _translate.GetById(id);
            if (tr == null)
                return NotFound();

            if (!IsAdmin && tr.Author != UserLogin)
                return Forbid();

            await _translate.Update(t => t.Id == id)
                .Set(t => t.Deleted, true)
                .Execute();

            TextTranslate newTr = null;

            if (!IsAdmin) // Удаляем все предыдущие переводы до первого чужого
            {
                TextTranslate next = tr;
                while (true)
                {
                    var prev = await _translate.Get(t => t.NextId == next.Id && !t.Deleted);
                    if (prev == null) break;

                    if (prev.Author == UserLogin)
                    {
                        await _translate.Update(t => t.Id == prev.Id)
                            .Set(t => t.Deleted, true)
                            .Execute();
                        next = prev;
                    }
                    else
                    {
                        await _translate.Update(t => t.Id == prev.Id)
                            .Set(t => t.NextId, null)
                            .Execute();
                        prev.NextId = null;
                        newTr = prev;
                        break;
                    }
                }
            }

            var another = await _translate.Query(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number && !t.Deleted && t.NextId == null);
            if (!another.Any())
            {
                await _texts.Update(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number)
                    .Set(t => t.HasTranslate, false)
                    .Set(t => t.TranslateApproved, false)
                    .Execute();

                await _translateService.UpdateVolumeProgress(tr.Project, tr.Volume);
                await _translateService.UpdateProjectProgress(tr.Project);
            }

            await _search.DeleteTranslate(id);

            return Ok(newTr != null ? new TranslateInfo(newTr) : null);
        }

        [HttpGet("{id}/history")]
        public async Task<ActionResult> History(string id)
        {
            var translate = await _translate.GetById(id);
            if (translate == null) return NotFound();

            if (!await HasAccessToProject(translate.Project)) return NotFound();

            var all = await _translate.Query(t => t.Project == translate.Project && t.Volume == translate.Volume && t.Number == translate.Number && t.NextId != null);

            var dict = all.ToDictionary(t => t.NextId, t => t);

            List<TranslateInfo> result = new()
            {
                new TranslateInfo(translate)
            };
            while (true)
            {
                if (!dict.TryGetValue(translate.Id, out var prev)) break;
                result.Add(new TranslateInfo(prev));
                translate = prev;
            }

            return Ok(result);
        }

        public class ApproveRequest
        {
            public bool Approved { get; set; }
        }

        [AuthAdmin]
        [HttpPost("{translateId}/approve")]
        public async Task<ActionResult> Approve(string translateId, [FromBody] ApproveRequest request)
        {
            var tr = await _translate.GetById(translateId);
            if (tr == null) return NotFound();

            await _texts.Update()
                .Where(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number)
                .Set(t => t.TranslateApproved, request.Approved)
                .Execute();

            await _translateService.UpdateVolumeProgress(tr.Project, tr.Volume);
            await _translateService.UpdateProjectProgress(tr.Project);

            return Ok();
        }
    }
}

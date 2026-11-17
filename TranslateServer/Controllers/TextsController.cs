using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Requests;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/volumes/{volume}/[controller]")]
    [ApiController]
    public class TextsController : ApiController
    {
        private readonly TextsStore _texts;
        private readonly TranslateStore _translate;
        private readonly VideoReferenceStore _references;
        private readonly CommentsStore _comments;
        private readonly CommentNotifyStore _commentNotify;
        private readonly VolumesStore _volumes;

        public TextsController(TextsStore texts,
            TranslateStore translate,
            VideoReferenceStore references,
            CommentsStore comments,
            CommentNotifyStore commentNotify,
            VolumesStore volumes,
            ProjectsStore projects)
        {
            _texts = texts;
            _translate = translate;
            _references = references;
            _comments = comments;
            _commentNotify = commentNotify;
            _volumes = volumes;
            _projects = projects;
        }

        [HttpGet]
        public async Task<ActionResult> List(string project, string volume)
        {
            // Texts
            var list = await _texts.Query()
                .Where(t => t.Project == project && t.Volume == volume)
                .SortAsc(t => t.Number)
                .Execute();

            // Translates
            var trList = await _translate.Query()
                .Where(t => t.Project == project && t.Volume == volume && t.NextId == null && !t.Deleted)
                .SortAsc(t => t.Number)
                .Execute();

            var comments = await _comments.Query(c => c.Project == project && c.Volume == volume);

            var tdict = trList.GroupBy(t => t.Number)
                .ToDictionary(
                    t => t.Key,
                    t => t.Select(tr => new TranslateInfo(tr, comments.Where(c => c.TranslateId == tr.FirstId || c.TranslateId == tr.Id))).ToArray()
                );

            // References
            var refs = await _references.Query(r => r.Project == project && r.Volume == volume && r.Rate != null);
            var rdict = refs.GroupBy(r => r.Number).ToDictionary(r => r.Key, g => g.Select(r => new
            {
                r.VideoId,
                r.Frame,
                T = Math.Max(r.T - 2, 0),
                r.Score,
                r.Rate,
            }));

            return Ok(list.Select(t => new
            {
                Source = t,
                Translates = tdict.TryGetValue(t.Number, out var tr) ? tr : null,
                Refs = rdict.TryGetValue(t.Number, out var r) ? r : null
            }));
        }

        public class CreateTextRequest
        {
            public string Text { get; set; }
        }

        [AuthAdmin]
        [HttpPost]
        public async Task<ActionResult> Create(string project, string volume, CreateTextRequest request)
        {
            if (!await HasAccessToProject(project)) return NotFound();

            var vol = await _volumes.Get(v => v.Project == project && v.Code == volume);
            if (vol == null) return NotFound();

            var last = await _texts.Collection
                .Find(t => t.Project == project && t.Volume == volume)
                .SortByDescending(t => t.Number)
                .Limit(1)
                .FirstOrDefaultAsync();

            var doc = new TextResource
            {
                Project = project,
                Volume = volume,
                Number = last == null ? 0 : last.Number + 1,
                Text = request?.Text ?? "",
                UserText = true,
            };
            doc.RecalcLetters();
            await _texts.Insert(doc);

            return Ok(new
            {
                Source = doc,
                Translates = (object)null,
                Refs = (object)null
            });
        }

        public class UpdateTextRequest
        {
            public string Text { get; set; }
        }

        [AuthAdmin]
        [HttpPost("{number}")]
        public async Task<ActionResult> Update(string project, string volume, int number, UpdateTextRequest request)
        {
            if (!await HasAccessToProject(project)) return NotFound();

            var txt = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == number);
            if (txt == null) return NotFound();
            if (!txt.UserText) return ApiBadRequest("Only user texts can be edited");

            var oldLetters = txt.Letters;
            txt.Text = request?.Text ?? "";
            txt.RecalcLetters();

            await _texts.Update(t => t.Id == txt.Id)
                .Set(t => t.Text, txt.Text)
                .Set(t => t.Letters, txt.Letters)
                .Execute();

            if (txt.Letters != oldLetters)
            {
                await _translate.Update(t => t.Project == project
                        && t.Volume == volume
                        && t.Number == number
                        && t.NextId == null
                        && !t.Deleted)
                    .Set(t => t.Letters, txt.Letters)
                    .ExecuteMany();
            }

            return Ok(txt);
        }

        [AuthAdmin]
        [HttpDelete("{number}")]
        public async Task<ActionResult> Delete(string project, string volume, int number)
        {
            if (!await HasAccessToProject(project)) return NotFound();

            var txt = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == number);
            if (txt == null) return NotFound();
            if (!txt.UserText) return ApiBadRequest("Only user texts can be deleted");

            var comments = await _comments.Query(c => c.Project == project && c.Volume == volume && c.Number == number);
            foreach (var comment in comments)
                await _commentNotify.Delete(n => n.CommentId == comment.Id);
            if (comments.Count > 0)
                await _comments.Delete(c => c.Project == project && c.Volume == volume && c.Number == number);

            var translates = await _translate.Query(t => t.Project == project && t.Volume == volume && t.Number == number && !t.Deleted);
            await _translate.Update(t => t.Project == project && t.Volume == volume && t.Number == number && !t.Deleted)
                .Set(t => t.Deleted, true)
                .ExecuteMany();

            await _texts.DeleteOne(t => t.Id == txt.Id);

            return Ok();
        }
    }
}

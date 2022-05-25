using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Requests;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/volumes/{volume}/[controller]")]
    [ApiController]
    public class TextsController : ApiController
    {
        private readonly TextsService _texts;
        private readonly TranslateService _translate;
        private readonly VideoReferenceService _references;
        private readonly CommentsService _comments;

        public TextsController(TextsService texts, TranslateService translate, VideoReferenceService references, CommentsService comments)
        {
            _texts = texts;
            _translate = translate;
            _references = references;
            _comments = comments;
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
            var refs = await _references.Query(r => r.Project == project && r.Volume == volume);
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
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;
using static System.Net.Mime.MediaTypeNames;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/volumes/{volume}/[controller]")]
    [ApiController]
    public class TextsController : ApiController
    {
        private readonly TextsService _texts;
        private readonly TranslateService _translate;

        public TextsController(TextsService texts, TranslateService translate)
        {
            _texts = texts;
            _translate = translate;
        }

        public class TranslateInfo
        {
            public TranslateInfo(TextTranslate tr)
            {
                Author = tr.Author;
                DateCreate = tr.DateCreate;
                Text = tr.Text;
            }

            public string Author { get; }
            public DateTime DateCreate { get; }
            public string Text { get; }
        }

        [HttpGet]
        public async Task<ActionResult> List(string project, string volume)
        {
            // Texts
            var list = await _texts.Query()
                .Where(t => t.Project == project && t.Volume == volume)
                .SortAsc(t => t.Number)
                .Execute();

            // My translates
            var myTrList = await _translate.Query()
                .Where(t => t.Project == project && t.Volume == volume && t.NextId == null && !t.Deleted && t.Author == UserLogin)
                .SortAsc(t => t.Number)
                .Execute();
            var myTDict = myTrList.ToDictionary(t => t.Number, t => new TranslateInfo(t));

            // Other translates
            var trList = await _translate.Query()
                .Where(t => t.Project == project && t.Volume == volume && t.NextId == null && !t.Deleted && t.Author != UserLogin)
                .SortAsc(t => t.Number)
                .Execute();
            var tdict = trList.GroupBy(t => t.Number).ToDictionary(t => t.Key, t => t.Select(tr => new TranslateInfo(tr)).ToArray());

            return Ok(list.Select(t => new
            {
                Source = t,
                My = myTDict.TryGetValue(t.Number, out var my) ? my : null,
                Translates = tdict.TryGetValue(t.Number, out var tr) ? tr : null,
            }));
        }
    }
}

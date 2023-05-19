using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SCI_Lib.Utils;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class ToolsController : ControllerBase
    {
        private readonly ILogger<ToolsController> _logger;
        private readonly TranslateStore _translate;
        private readonly TextsStore _texts;
        private readonly SearchService _search;

        public ToolsController(ILogger<ToolsController> logger, TranslateStore translate, TextsStore texts, SearchService search)
        {
            _logger = logger;
            _translate = translate;
            _texts = texts;
            _search = search;
        }

        [HttpPost("import/{from}/{to}")]
        public async Task<ActionResult> ImportTranslate(string from, string to)
        {
            _logger.LogInformation($"Import translate from {from} to {to}");

            //await _translate.Delete(t => t.Project == to);
            var srcTexts = await _texts.Query(t => t.Project == from);
            var dstTexts = await _texts.Query(t => t.Project == to);
            var srcTranslates = await _translate.Query(t => t.Project == from && !t.Deleted && t.NextId == null);
            var dstTranslates = await _translate.Query(t => t.Project == to && !t.Deleted && t.NextId == null);

            foreach (var byVolume in srcTexts.GroupBy(t => t.Volume))
            {
                var volTr = dstTranslates.Where(t => t.Volume == byVolume.Key);
                foreach (var gr in byVolume.GroupBy(t => t.Text)) // Чтобы избежать дублирования. Если один и тот же исходный текст встречается несколько раз
                {
                    var txt = gr.First();
                    bool exact = true;

                    var sameTexts = dstTexts.Where(t => t.Volume == txt.Volume && t.Text == txt.Text);
                    if (!sameTexts.Any())
                    {
                        // Пытаемся найти похожий текст
                        var searchResult = await _search.SearchInProject(to, txt.Text, true, false, 0, 10);

                        if (searchResult.Any())
                        {
                            double best = 0;
                            string bestTxt = null;
                            foreach (var res in searchResult.Where(r => r.Volume == txt.Volume))
                            {
                                var score = res.Score.GetValueOrDefault(0) / txt.MaxScore.GetValueOrDefault(1);
                                if (best < score)
                                {
                                    best = score;
                                    bestTxt = res.Html;
                                }

                                if (score > 0.70)
                                {
                                    sameTexts = dstTexts.Where(t => t.Volume == txt.Volume && t.Number == res.Number);
                                    exact = false;
                                    break;
                                }
                            }

                            if (!sameTexts.Any())
                                System.Console.WriteLine($"{txt.Text}\n{bestTxt}\nBest score: {best}\n\n");
                        }
                    }

                    if (!sameTexts.Any())
                        continue;

                    foreach (var dst in sameTexts)
                    {
                        await _texts.Update(t => t.Id == dst.Id)
                            .Set(t => t.TranslateApproved, txt.TranslateApproved)
                            .Execute();
                    }

                    var tr = srcTranslates.Find(t => t.Volume == txt.Volume && t.Number == txt.Number);
                    if (tr == null) continue;

                    foreach (var dst in sameTexts)
                    {
                        if (volTr.Any(t => t.Number == dst.Number)) continue; // Пропускаем уже переведённое

                        if (!exact)
                            System.Console.WriteLine($"{txt.Text}\n{dst.Text}\n{tr.Text}\n\n");

                        await _translate.Insert(new TextTranslate
                        {
                            Project = to,
                            Volume = dst.Volume,
                            Number = dst.Number,
                            Text = tr.Text,
                            Author = tr.Author,
                            Editor = tr.Editor,
                            DateCreate = tr.DateCreate,
                            Letters = tr.Letters,
                        });
                    }
                }
            }

            _logger.LogInformation("Import complete");
            return Ok();
        }

        [HttpPost("fix/{project}")]
        public async Task<ActionResult> Fix(string project)
        {
            var translates = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null);
            foreach (var byNum in translates.GroupBy(t => t.Volume + t.Number))
            {
                if (byNum.Count() > 1)
                {
                    var arr = byNum.ToArray();
                    for (int i = 1; i < arr.Length; i++)
                    {
                        await _translate.DeleteOne(t => t.Id == arr[i].Id);
                    }
                }
            }
            return Ok();
        }

        [HttpPost("said/{project}/{num}")]
        public async Task<ActionResult> ExtractSaids(string project, ushort num, [FromServices] SCIService sci)
        {
            var package = sci.Load(project);
            var extract = new SaidExtract(package);
            var saids = extract.Process(num);
            var volume = $"text_{num:D3}";
            for (int i = 0; i < saids.Length; i++)
            {
                var said = saids[i];
                await _texts.Update(t => t.Project == project && t.Volume == volume && t.Number == i)
                    .Set(t => t.Description, said)
                    .Execute();
            }

            return Ok();
        }
    }
}

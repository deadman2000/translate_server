﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SCI_Lib;
using SCI_Lib.Analyzer;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Elements;
using SCI_Lib.Resources.Scripts.Sections;
using SCI_Lib.Resources.Vocab;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Helpers;
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
        private readonly ProjectsStore _project;
        private readonly VolumesStore _volumes;
        private readonly TranslateStore _translate;
        private readonly TextsStore _texts;
        private readonly SearchService _search;
        private readonly SCIService _sci;
        private readonly WordsStore _words;
        private readonly SuffixesStore _suffixes;
        private readonly SaidStore _saids;
        private readonly SynonymStore _synonyms;

        public ToolsController(ILogger<ToolsController> logger,
            ProjectsStore project,
            TranslateStore translate,
            VolumesStore volumes,
            TextsStore texts,
            SearchService search,
            SCIService sci,
            WordsStore words,
            SuffixesStore suffixes,
            SaidStore saids,
            SynonymStore synonyms
        )
        {
            _logger = logger;
            _project = project;
            _volumes = volumes;
            _translate = translate;
            _texts = texts;
            _search = search;
            _sci = sci;
            _words = words;
            _suffixes = suffixes;
            _saids = saids;
            _synonyms = synonyms;
        }

        /// <summary>
        /// Переносит перевод с одного проекта в другой
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
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

        [HttpPost("said/{project}/{num}")]
        public async Task<ActionResult> ExtractSaids(string project, ushort num)
        {
            var package = _sci.Load(project);
            var extract = new SaidExtractWeb(package);
            var saids = extract.Process(num);
            var volume = $"text_{num:D3}";
            for (int ind = 0; ind < saids.Length; ind++)
            {
                var said = saids[ind];
                await _texts.Update(t => t.Project == project && t.Volume == volume && t.Number == ind)
                    .Set(t => t.Description, said)
                    .Execute();
            }

            return Ok();
        }

        [HttpPost("said2/{project}")]
        public async Task<ActionResult> ExtractSaids2(string project)
        {
            ushort num = 210;
            var verbCnt = 9;
            var msgOffset = 0;
            var verbOffset = 76;
            var nounOffset = 85;
            var rangesOffset = 123;

            var package = _sci.Load(project);
            var res = package.GetResource<ResScript>(num);
            var scr = res.GetScript() as Script;
            var vars = scr.Get<LocalVariablesSection>().First().Vars;

            for (int verbInd = 0; verbInd <= verbCnt; verbInd++)
            {
                var from = (ushort)vars[rangesOffset + verbInd * 2];
                var to = from + (ushort)vars[rangesOffset + verbInd * 2 + 1];
                for (int noun = from; noun < to; noun++)
                {
                    var txtRes = (ushort)vars[msgOffset + noun * 2];
                    var txtInd = (ushort)vars[msgOffset + noun * 2 + 1];
                    var verb = ((SaidExpression)((RefToElement)vars[verbOffset + verbInd]).Reference).Label.TrimEnd('>');
                    var volume = $"text_{txtRes:D3}";
                    var said = $"{verb}{vars[nounOffset + noun]}";

                    System.Console.WriteLine($"{said}  {txtRes}:{txtInd}");
                    await _texts.Update(t => t.Project == project && t.Volume == volume && t.Number == txtInd)
                        .Set(t => t.Description, said)
                        .Execute();
                }
            }

            return Ok();
        }

        [HttpPost("tells")]
        public async Task<ActionResult> Tells()
        {
            Dictionary<string, string> translates = new()
            {
                {"bed", "кровать" },
                {"carpet", "ковер" },
                {"celie", "сели" },
                {"chair", "стул" },
                {"clarence", "кларенса" },
                {"colonel", "полковника" },
                {"couch", "диван" },
                {"crank", "рычаг" },
                {"door", "дверь" },
                {"elevator", "лифт" },
                {"equipment", "прачечную" },
                {"ethel", "этель" },
                {"eye", "глаз" },
                {"fifi", "фифи" },
                {"fire", "огонь" },
                {"fireplace", "камин" },
                {"floor", "пол" },
                {"gertie", "герти" },
                {"gloria", "глорию" },
                {"ground", "землю" },
                {"horse", "лошадь" },
                {"house", "дом" },
                {"jeeves", "дживса" },
                {"lamp", "лампу" },
                {"lillian", "лилиан" },
                {"mantel", "каминную полку" },
                {"mirror", "зеркало" },
                {"owl", "сову" },
                {"picture", "картину" },
                {"platform", "платформу" },
                {"rudy", "руди" },
                {"sofa", "софу" },
                {"table", "стол" },
                {"wilbur", "уилбура" },
                {"window", "окно" },
            };

            var package = _sci.Load("colonels_bequest");

            foreach (var res in package.GetResources<ResScript>())
            {
                var volume = $"script_{res.Number:D3}";
                var scr = res.GetScript() as Script;
                var strings = scr.AllStrings().ToList();
                foreach (var cs in scr.Get<CodeSection>())
                {
                    foreach (var op in cs.Operators)
                    {
                        if (op.Name == "callb")
                        {
                            if ((byte)op.Arguments[0] == 0x19 && (byte)op.Arguments[1] == 2)
                            {
                                var r = op.Prev.Prev.Arguments[0] as CodeRef;
                                var s = r.Reference as StringConst;
                                var ind = strings.IndexOf(s);
                                Console.WriteLine($"{res.Number}:{ind}  {s.Value}");

                                if (!translates.TryGetValue(s.Value, out var translate)) continue;

                                var tr = await _translate.Get(t => t.Project == "colonels_bequest" && !t.Deleted && t.NextId == null
                                    && t.Volume == volume && t.Number == ind);

                                if (tr == null) throw new Exception();
                                if (tr.Text != s.Value)
                                    Console.WriteLine($"{tr.Text} != {s.Value}");

                                await _translate.Update(t => t.Id == tr.Id).Set(t => t.Text, translate).Execute();
                            }
                        }
                    }
                }
            }

            return Ok();
        }

        [HttpPost("escape_check")]
        public async Task<ActionResult> EscapeCheck()
        {
            foreach (var tr in await _translate.Query(t => !t.Deleted))
            {
                if (tr.Text.Contains('$'))
                {
                    Console.WriteLine($"{tr.Project} {tr.Volume} {tr.Number}: {tr.Text}");
                    if (tr.Text.Contains("$$"))
                    {
                        Console.WriteLine("$$ IGNORE");
                        continue;
                    }

                    var newText = tr.Text.Replace("$", "$$");
                    var isTranslate = tr.IsTranslate;
                    var en = await _texts.Get(t => t.Project == tr.Project & t.Volume == tr.Volume && t.Number == tr.Number);
                    if (en != null)
                        isTranslate = en.Text != newText;

                    await _translate.Update(t => t.Id == tr.Id)
                        .Set(t => t.Text, newText)
                        .Set(t => t.IsTranslate, isTranslate)
                        .Execute();
                }
            }
            return Ok();
        }

        [HttpPost("escape_tr")]
        public async Task<ActionResult> EscapeTr()
        {
            var projects = await _project.All();
            foreach (var pr in projects)
            {
                var volumes = await _volumes.Query(v => v.Project == pr.Code);
                foreach (var vol in volumes)
                {
                    var texts = await _texts.Query(t => t.Project == pr.Code && t.Volume == vol.Code);
                    foreach (var txt in texts)
                    {
                        if (txt.Text.Contains('$'))
                        {
                            var tr = await _translate.Get(t => t.Project == txt.Project && t.Volume == txt.Volume && t.Number == txt.Number && !t.Deleted && t.NextId == null);
                            if (tr != null && !tr.Text.Contains('$'))
                            {
                                await Console.Out.WriteLineAsync($"{tr.Project}:{tr.Volume}:{tr.Number} {tr.Text}");
                            }
                        }
                    }
                }
            }
            return Ok();
        }

        static List<string> PrintFuncs(string project)
        {
            List<string> proc = new();
            if (project == "camelot")
                proc.Add("proc_14");
            return proc;
        }

        [HttpPost("parser/{project}")]
        public async Task<ActionResult> ExtractParser(string project)
        {
            _logger.LogInformation($"{project} Begin extract parser");

            var package = _sci.Load(project);

            var scriptRes = package.Scripts
                .GroupBy(r => r.Number).Select(g => g.First());

            var scripts = scriptRes.Select(r => r.GetScript() as Script)
                .Where(s => s != null)
                .ToList();
            if (!scripts.Any()) return BadRequest();

            _logger.LogInformation($"{project} Find prints");
            TextUsageSearch usage = new(package);
            var calls = usage.FindUsage(PrintFuncs(project));

            _logger.LogInformation($"{project} Setup texts saids");
            foreach (var p in calls)
            {
                IEnumerable<SaidExpression> saids = p.Saids;
                foreach (var said in saids)
                    said.Normalize();
                saids = saids.Where(s => s.Label != "kiss/angel>");

                var volume = $"text_{p.Txt:D3}";
                var descr = string.Join('\n', saids.Select(s => s.Label));

                await _texts.Update(t => t.Project == project && t.Volume == volume && t.Number == p.Index)
                    .Set(t => t.Description, descr)
                    .Execute();
            }

            {
                _logger.LogInformation($"{project} Extract saids");
                await _saids.Delete(s => s.Project == project);
                await _synonyms.Delete(s => s.Project == project);

                var printMap = calls
                    .SelectMany(c => c.Saids.Select(s => new { s, c }))
                    .GroupBy(o => o.s)
                    .ToDictionary(gr => gr.Key,
                        gr => string.Join(',', gr.Select(o => $"{o.c.Txt}.{o.c.Index}")));

                foreach (var scr in scripts)
                {
                    var ss = scr.SaidSection;
                    if (ss == null) continue;

                    for (int i = 0; i < ss.Saids.Count; i++)
                    {
                        var expr = ss.Saids[i];
                        await _saids.Insert(new SaidDocument
                        {
                            Project = project,
                            Script = scr.Resource.Number,
                            Index = i,
                            Expression = expr.Label,
                            Prints = printMap.GetValueOrDefault(expr, null)
                        });
                    }

                    var synSec = scr.Get<SynonymSecion>().FirstOrDefault();
                    if (synSec != null)
                    {
                        for (int i = 0; i < synSec.Synonyms.Count; i++)
                        {
                            var s = synSec.Synonyms[i];
                            await _synonyms.Insert(new SynonymDocument
                            {
                                Project = project,
                                Script = scr.Resource.Number,
                                Index = i,
                                WordA = s.WordA,
                                WordB = s.WordB,
                                Delete = s.WordA == s.WordB
                            });
                        }
                    }
                }
            }

            Dictionary<ushort, string> wordsUsage;
            {
                _logger.LogInformation($"{project} Build words usage map");
                wordsUsage = scripts
                    .SelectMany(s => s.Get<SaidSection>().SelectMany(ss => ss.Saids)
                            .SelectMany(s => s.Expression)
                            .Where(e => !e.IsOperator)
                            .Select(s => s.Data)
                            .Union(s.Get<SynonymSecion>().SelectMany(s => s.Synonyms).Select(s => s.WordA))
                            .Distinct()
                            .Select(w => new { S = s, W = w })
                    )
                    .GroupBy(i => i.W)
                    .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(n => n.S.Resource.Number.ToString())));
            }
            if (!wordsUsage.Any()) return BadRequest();

            {
                _logger.LogInformation($"{project} Extract words");
                if (package.GetResource<ResVocab>(0) is not ResVocab000 voc) return BadRequest();

                await _words.Delete(w => w.Project == project && !w.IsTranslate);

                var words = voc.GetWords();
                foreach (var gr in words.GroupBy(w => w.Id))
                {
                    await _words.Insert(new WordDocument
                    {
                        Project = project,
                        Usage = wordsUsage.GetValueOrDefault(Word.GetGroup(gr.Key), ""),
                        WordId = gr.Key,
                        Text = string.Join(", ", gr.Select(w => w.Text)),
                        IsTranslate = false,
                    });
                }
            }

            {
                _logger.LogInformation($"{project} Extract suffixes");
                await _suffixes.Delete(s => s.Project == project && !s.IsTranslate);
                if (package.GetResource<ResVocab>(901) is not ResVocab901 voc) return BadRequest();
                foreach (var s in voc.GetSuffixes())
                {
                    await _suffixes.Insert(new SuffixDocument
                    {
                        Project = project,
                        IsTranslate = false,
                        Input = s.Pattern,
                        InClass = (int)s.InputClass,
                        Output = s.Output,
                        OutClass = (int)s.SuffixClass,
                    });
                }
            }

            await _project.Update(p => p.Code == project).Set(p => p.HasSaid, true).Execute();
            _logger.LogInformation($"{project} Extract parser completed!");

            return Ok();
        }

        [HttpPost("import")]
        public async Task<ActionResult> Import()
        {
            var project = "camelot";
            var package = _sci.Load(project);
            var translate = SCIPackage.Load(@"D:\Dos\GAMES\Conquests_of_Camelot_rus\");

            {
                _logger.LogInformation($"{project} Import words");
                var wordsVoc = (ResVocab001)translate.GetResource(ResType.Vocabulary, 1);
                await _words.Delete(w => w.Project == project && w.IsTranslate);
                var words = wordsVoc.GetWords();
                foreach (var gr in words.GroupBy(w => w.Id))
                {
                    await _words.Insert(new WordDocument
                    {
                        Project = project,
                        WordId = gr.Key,
                        Text = string.Join(", ", gr.Select(w => w.Text)),
                        IsTranslate = true,
                    });
                }
            }

            {
                _logger.LogInformation($"{project} Import suffixes");
                var suffVoc = (ResVocab901)translate.GetResource(ResType.Vocabulary, 901);
                await _suffixes.Delete(s => s.Project == project && s.IsTranslate);
                foreach (var suff in suffVoc.GetSuffixes())
                {
                    if (IsTranslate(suff.Pattern) || IsTranslate(suff.Output))
                    {
                        await _suffixes.Insert(new SuffixDocument
                        {
                            Project = project,
                            IsTranslate = true,
                            Input = suff.Pattern,
                            InClass = (int)suff.InputClass,
                            Output = suff.Output,
                            OutClass = (int)suff.SuffixClass,
                        });
                    }
                }
            }

            {
                _logger.LogInformation($"{project} Import saids");
                await _saids.Update(s => s.Project == project).Set(s => s.Patch, null).ExecuteMany();

                var scriptRes = package.Scripts
                    .GroupBy(r => r.Number).Select(g => g.First());
                var scripts = scriptRes.Select(r => r.GetScript() as Script)
                    .Where(s => s != null)
                    .ToList();
                foreach (var scr in scripts)
                {
                    var ss = scr.SaidSection;
                    if (ss == null) continue;

                    var trScr = translate.GetResource<ResScript>(scr.Resource.Number).GetScript() as Script;
                    var trSS = trScr.SaidSection;

                    for (int i = 0; i < ss.Saids.Count; i++)
                    {
                        if (!SaidExpression.IsEqual(trSS.Saids[i].Expression, ss.Saids[i].Expression))
                        {
                            await _saids.Update(s => s.Project == project && s.Script == scr.Resource.Number && s.Index == i)
                                .Set(s => s.Patch, trSS.Saids[i].Label)
                                .Execute();
                        }
                    }
                }
            }

            return Ok();
        }

        private static bool IsTranslate(string str) => str.Any(c => c > 127);

        [HttpPost("gob")]
        [AllowAnonymous]
        public async Task<ActionResult> Gob()
        {
            var proj = "gobliiins_5";
            var allTxt = await _texts.Query(t => t.Project == proj);
            var allTr = await _translate.Query(t => t.Project == proj && !t.Deleted && t.NextId == null);

            HashSet<string> skip = new();

            foreach (var tr in allTr)
            {
                var txt = allTxt.FirstOrDefault(t => t.Volume == tr.Volume && t.Number == tr.Number);
                if (txt == null) continue;
                if (skip.Contains(txt.Description)) continue;

                var other = allTxt.Where(t => t.Description == txt.Description && t.Volume != txt.Volume);

                foreach (var t2 in other)
                {
                    if (allTr.Exists(t => t.Volume == t2.Volume && t.Number == t2.Number)) continue;

                    await Console.Out.WriteLineAsync($"{txt.Volume} => {t2.Volume}   {txt.Text} {t2.Text} => {tr.Text}");
                    //await _translateService.Submit(proj, t2.Volume, t2.Number, tr.Text, "system", true);
                    skip.Add(txt.Description);
                }
            }


            return Ok();
        }
    }
}

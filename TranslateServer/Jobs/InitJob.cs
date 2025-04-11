using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Quartz;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Sections;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Model.Yandex;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Jobs
{
    class InitJob : IJob
    {
        public static void Schedule(IServiceCollectionQuartzConfigurator q)
        {
            q.ScheduleJob<InitJob>(j => j.StartNow());
        }

        private readonly ILogger<InitJob> _logger;
        private readonly UsersStore _users;
        private readonly ProjectsStore _projects;
        private readonly TranslateStore _translate;
        private readonly YandexSpellcheck _spellcheck;
        private readonly TextsStore _texts;
        private readonly SCIService _sci;
        private readonly SaidStore _saids;
        private readonly SpellcheckCache _spellcheckCache;

        public InitJob(ILogger<InitJob> logger,
            UsersStore users,
            ProjectsStore projects,
            TranslateStore translate,
            YandexSpellcheck spellcheck,
            TextsStore texts,
            SCIService sci,
            SaidStore saids,
            SpellcheckCache spellcheckCache)
        {
            _logger = logger;
            _users = users;
            _projects = projects;
            _translate = translate;
            _spellcheck = spellcheck;
            _texts = texts;
            _sci = sci;
            _saids = saids;
            _spellcheckCache = spellcheckCache;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await UsersInit();

            //await CheckDublicates("longbow_1_1");
            //await EscapeStrings();
            //await UpdateNullEngine();
            //await RepairHasTranslate("larry_5");
            //await Spellchecking();
            await FileNamesToUpper();
            _logger.LogInformation("Init complete");
        }

        private async Task FileNamesToUpper()
        {
            var projects = await _projects.All();
            foreach (var project in projects)
            {
                var dir = _sci.GetProjectPath(project.Code);

                if (!Directory.Exists(dir))
                {
                    continue;
                }

                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName != fileName.ToUpper())
                    {
                        File.Move(file, Path.Combine(dir, fileName.ToUpper()));
                    }
                }
            }
        }

        private async Task RepairHasTranslate(string project)
        {
            var allTranslates = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null);

            var trMap = allTranslates.Select(t => $"{t.Volume}.{t.Number}").ToHashSet();

            var allTexts = await _texts.Query(t => t.Project == project);
            foreach (var txt in allTexts)
            {
                string k = $"{txt.Volume}.{txt.Number}";
                var hasTranslate = trMap.Contains(k);
                if (txt.HasTranslate != hasTranslate)
                {
                    await Console.Out.WriteLineAsync($"Fixed {txt.Volume} {txt.Number} {txt.Text} {hasTranslate}");
                    await _texts.Update(t => t.Id == txt.Id).Set(t => t.HasTranslate, hasTranslate).Execute();
                }
                if (!hasTranslate && txt.TranslateApproved)
                {
                    await Console.Out.WriteLineAsync($"Fixed approve {txt.Volume} {txt.Number} {txt.Text}");
                    await _texts.Update(t => t.Id == txt.Id).Set(t => t.TranslateApproved, false).Execute();
                }
            }
            await Console.Out.WriteLineAsync($"Fix RepairHasTranslate Completed");
        }

        private async Task RemoveTalkerSrc(string project)
        {
            var package = await _sci.Load(project);
            foreach (var res in package.GetResources<ResScript>())
            {
                var scr = res.GetScript() as Script;
                var instances = scr.Get<ClassSection>().Where(c => c.SuperClass != null && c.SuperClass.Name == "Talker");
                if (instances.Any())
                {
                    var names = instances.Select(i => i.Name).ToHashSet();

                    var vol = Volume.FileNameToCode(res.FileName);

                    var trs = await _translate.Query(t => t.Project == project && t.Volume == vol && !t.Deleted && t.NextId == null && !t.IsTranslate);
                    foreach (var tr in trs)
                    {
                        if (names.Contains(tr.Text))
                        {
                            await _translate.DeleteOne(t => t.Id == tr.Id);
                            await _texts.Update(t => t.Project == project && t.Volume == tr.Volume && t.Number == tr.Number)
                                .Set(t => t.HasTranslate, false)
                                .Execute();
                            await Console.Out.WriteLineAsync($"{tr.Volume} {tr.Number} {tr.Text}");
                        }
                    }
                }
            }

            await Console.Out.WriteLineAsync();
        }

        private Task UpdateNullEngine()
        {
            return _projects.Update(p => p.Engine == null).Set(p => p.Engine, "sci").ExecuteMany();
        }

        private async Task EscapeStrings()
        {
            var projects = await _projects.All();
            foreach (var proj in projects)
                await Escape(proj);
        }

        private async Task SetupIsTranslate(string project)
        {
            //private static bool IsTranslate(string str) => str.Any(c => c > 127);
            var translates = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null && !t.IsTranslate);
            foreach (var tx in translates)
            {
                if (tx.Text.Any(c => c > 127))
                {
                    await Console.Out.WriteLineAsync(tx.Text);
                    await _translate.Update(t => t.Id == tx.Id).Set(t => t.IsTranslate, true).Execute();
                }
            }
        }

        private async Task CheckDublicates(string project)
        {
            await SetupIsTranslate(project);
            var translates = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null && t.IsTranslate);

            // Multiple check
            var multipleTranslate = translates.Select(t => new { VN = t.Volume + t.Number, t })
                .GroupBy(v => v.VN)
                .Where(g => g.Count() > 1);
            if (multipleTranslate.Any())
            {
                foreach (var gr in multipleTranslate)
                {
                    await Console.Out.WriteLineAsync(gr.Key);
                    foreach (var v in gr)
                    {
                        await Console.Out.WriteLineAsync(v.t.Text);
                    }
                }
                return;
            }

            var texts = await _texts.Query(t => t.Project == project);
            var dublicateSources = texts.GroupBy(t => t.Text).Where(g => g.Count() > 1);
            foreach (var gr in dublicateSources)
            {
                var h = gr.Select(t => t.Volume + t.Number).ToHashSet();
                var trs = translates.Where(t => h.Contains(t.Volume + t.Number)).ToList();
                if (trs.Select(t => t.Text).Distinct().Count() > 1)
                {
                    await Console.Out.WriteLineAsync(gr.Key);
                    foreach (var tr in trs)
                        await Console.Out.WriteLineAsync($"   {tr.Volume} {tr.Number} {tr.Text}");
                }
            }

            await Console.Out.WriteLineAsync();
        }


        public async Task Escape(Project project)
        {
            try
            {
                var package = _sci.Load(project);

                foreach (var res in package.GetResources<ResText>())
                    await Escape(project.Code, res);
                foreach (var res in package.GetResources<ResScript>())
                    await Escape(project.Code, res);
                foreach (var res in package.GetResources<ResMessage>())
                    await EscapeMsg(project.Code, res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Escape error");
            }
        }

        private async Task Escape(string project, Resource res)
        {
            var enc = res.Package.GameEncoding;
            var vol = Volume.FileNameToCode(res.FileName);
            var texts = await _texts.Query(t => t.Project == project && t.Volume == vol);
            var dict = texts.ToDictionary(t => t.Number, t => t);

            var strings = res.GetStrings();
            for (int i = 0; i < strings.Length; i++)
            {
                if (!dict.TryGetValue(i, out var txt)) continue;

                var esc = enc.EscapeString(strings[i]);
                if (txt.Text != esc)
                {
                    _logger.LogWarning($"Escaped {project} {res.FileName} {i}");
                    await _texts.Update(t => t.Id == txt.Id)
                        .Set(t => t.Text, esc)
                        .Execute();
                }
            }
        }

        private async Task EscapeMsg(string project, ResMessage res)
        {
            var enc = res.Package.GameEncoding;
            var vol = Volume.FileNameToCode(res.FileName);
            var texts = await _texts.Query(t => t.Project == project && t.Volume == vol);
            var dict = texts.ToDictionary(t => t.Number, t => t);

            var records = res.GetMessages();

            for (int i = 0; i < records.Count; i++)
            {
                if (!dict.TryGetValue(i, out var txt)) continue;

                var esc = enc.EscapeString(records[i].Text);
                if (txt.Text != esc)
                {
                    _logger.LogWarning($"Escaped {project} {res.FileName} {i}");
                    await _texts.Update(t => t.Id == txt.Id)
                        .Set(t => t.Text, esc)
                        .Execute();
                }
            }
        }

        private async Task UsersInit()
        {
            var cnt = await _users.Collection.CountDocumentsAsync(u => true);
            if (cnt > 0)
                return;

            var user = new UserDocument
            {
                Login = "admin",
                Role = UserDocument.ADMIN
            };
            user.SetPassword("admin");
            await _users.Insert(user);
            _logger.LogWarning("Created user 'admin' with password 'admin'");
        }

        private async Task Spellchecking()
        {
            _logger.LogInformation("Spell checking...");

            var projects = (await _projects.All()).Select(p => p.Code);
            foreach (var proj in projects)
            {
                var translates = (await _translate.Query(t => t.Project == proj && !t.Deleted && t.NextId == null && t.Spellcheck == null && t.IsTranslate == true)).ToArray();
                if (translates.Length == 0) continue;

                var texts = translates.Select(t => t.Text).ToArray();

                var result = _spellcheck.Spellcheck(texts);
                int i = 0;
                await foreach (var res in result)
                {
                    var spellcheck = res;
                    var tr = translates[i];

                    if (res.Any())
                    {
                        var txt = await _texts.Get(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number);
                        if (txt.Text == tr.Text)
                            spellcheck = Array.Empty<SpellResult>();
                    }

                    await _translate.Update(t => t.Id == tr.Id)
                        .Set(t => t.Spellcheck, spellcheck)
                        .Execute();
                    i++;
                }
            }

            _spellcheckCache.ResetTotal();
            _logger.LogInformation("Spell checking done");
        }
    }
}

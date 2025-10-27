using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Quartz;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Sections;
using SCI_Lib.Resources.Scripts1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly VolumesStore _volumes;
        private readonly SCIService _sci;
        private readonly SaidStore _saids;
        private readonly WordsStore _words;
        private readonly SynonymStore _synonym;
        private readonly SuffixesStore _suffixes;
        private readonly SpellcheckCache _spellcheckCache;
        private readonly TranslateService _translateService;
        private readonly PatchesStore _patches;

        public InitJob(ILogger<InitJob> logger,
            UsersStore users,
            ProjectsStore projects,
            TranslateStore translate,
            YandexSpellcheck spellcheck,
            TextsStore texts,
            VolumesStore volumes,
            SCIService sci,
            SaidStore saids,
            WordsStore words,
            SynonymStore synonym,
            SuffixesStore suffixes,
            SpellcheckCache spellcheckCache,
            TranslateService translateService,
            PatchesStore patches)
        {
            _logger = logger;
            _users = users;
            _projects = projects;
            _translate = translate;
            _spellcheck = spellcheck;
            _texts = texts;
            _volumes = volumes;
            _sci = sci;
            _saids = saids;
            _words = words;
            _synonym = synonym;
            _suffixes = suffixes;
            _spellcheckCache = spellcheckCache;
            _translateService = translateService;
            _patches = patches;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await UsersInit();
            await FileNamesToUpper();

            // await AddText("freddy_pharkas_cd", 660, "Damned flies!  This place is like a stable!");

            //await AddMessageLabel("freddy_pharkas_cd");
            // await ParenthesesPatch("freddy_pharkas_cd");
            //await CheckDublicates("longbow_1_1");
            //await EscapeStrings();
            //await UpdateNullEngine();
            //await RepairHasTranslate("larry_5");
            //await Spellchecking();
            //await CopyParser("larry_3_pnc_v2", "larry_3");
            _logger.LogInformation("Init complete");
        }

        private async Task AddMessageLabel(string projectCode)
        {
            var project = await _projects.GetProject(projectCode);
            var package = _sci.Load(project);

            //Применяем патчи
            var patches = (await _patches.Query(p => p.Project == projectCode && !p.Deleted)).ToList();
            foreach (var p in patches)
            {
                var data = await _patches.GetContent(p.FileId);
                if (p.FileName.ToLower().EndsWith(".msg"))
                {
                    package.SetPatch(p.FileName.ToUpper(), data);
                }
            }

            Dictionary<ushort, string> objects = new();
            {
                var resInv = package.GetResource<ResScript>(15);
                if (resInv.GetScript() is Script1 scr)
                {
                    foreach (var obj in scr.Objects)
                    {
                        if (obj.HasProperty("message"))
                        {
                            var msg = obj.GetProperty("message");
                            if (msg != 65535)
                            {
                                objects[msg] = obj.Name;
                            }
                        }
                    }
                }
            }

            foreach (var res in package.GetResources<ResMessage>())
            {
                Console.WriteLine(res.Number);

                Dictionary<ushort, string> nouns = new();
                var resScr = package.GetResource<ResScript>(res.Number);
                if (resScr != null)
                {
                    if (resScr.GetScript() is Script1 scr)
                    {
                        foreach (var obj in scr.Objects)
                        {
                            if (obj.HasProperty("noun"))
                            {
                                var noun = obj.GetProperty("noun");
                                nouns[noun] = obj.Name;
                            }
                        }
                    }
                }

                var messages = res.GetMessages();
                for (int i = 0; i < messages.Count; i++)
                {
                    var msg = messages[i];

                    string verb;
                    if (msg.Verb == 0)
                        verb = "0";
                    else if (objects.TryGetValue(msg.Verb, out var name))
                        verb = $"{name} ({msg.Verb})";
                    else
                        verb = msg.Verb.ToString();

                    if (nouns.TryGetValue(msg.Noun, out string noun))
                        noun = $"{noun} ({msg.Noun})";
                    else
                        noun = msg.Noun.ToString();

                    await _texts.Update(t => t.Project == projectCode && t.Volume == $"{res.Number}_msg" && t.Number == i)
                        .Set(t => t.Description, $"Noun: {noun} Verb: {verb} Cond: {msg.Cond} Seq: {msg.Seq} Talker: {msg.Talker}")
                        .Execute();
                }
            }
        }

        private async Task AddText(string projectCode, int messageNumber, string text)
        {
            var project = await _projects.GetProject(projectCode);
            var volume = await _volumes.Get(v => v.Project == project.Code && v.Code == $"{messageNumber}_msg");
            var index = (await _texts.Query(t => t.Project == project.Code && t.Volume == volume.Code)).Select(t => t.Number).Max() + 1;
            await _texts.Insert(new TextResource(project, volume, index++, text));
            Console.WriteLine("Text added");
        }

        private async Task ParenthesesPatch(string project)
        {
            var translates = await _translate.Query(t => t.Project == project && t.IsTranslate && t.NextId == null && !t.Deleted);

            var pattern = new Regex("\\([^A-Za-z0-9]+\\)");

            (char, char)[] mapping = new[] {
                ('A', 'А'),
                ('C', 'С'),
                ('E', 'Е'),
                ('H', 'Н'),
                ('K', 'К'),
                ('O', 'О'),
                ('P', 'Р'),
                ('T', 'Т'),
                ('X', 'Х'),
                ('a', 'а'),
                ('c', 'с'),
                ('e', 'е'),
                ('o', 'о'),
                ('x', 'х'),
            };

            foreach (var t in translates)
            {
                var result = pattern.Match(t.Text);
                if (result.Success)
                {
                    var newPart = result.Value;
                    foreach (var (to, from) in mapping)
                    {
                        newPart = newPart.Replace(from, to);
                    }

                    if (newPart == result.Value)
                    {
                        Console.WriteLine(result.Value);
                        continue;
                    }

                    var newText = t.Text.Replace(result.Value, newPart);

                    await _translateService.Submit(t.Project, t.Volume, t.Number, newText, "admin", true, t.Id);
                }
            }

            Console.WriteLine();
        }

        private async Task CopyParser(string project, string fromProject)
        {
            // Words
            /*var words = await _words.Query(w => w.Project == fromProject && w.IsTranslate);

            await _words.Insert(words.Select(w => new WordDocument
            {
                Project = project,
                Text = w.Text,
                WordId = w.WordId,
                IsTranslate = true,
            }));

            // Synonyms
            var createdSynonyms = await _synonym.Query(s => s.Project == fromProject && s.Index == null);
            await _synonym.Insert(createdSynonyms.Select(s => new SynonymDocument
            {
                Project = project,
                Script = s.Script,
                WordA = s.WordA,
                WordB = s.WordB,
            }));

            var deletedSynonyms = await _synonym.Query(s => s.Project == fromProject && s.Delete);
            foreach (var syn in deletedSynonyms)
            {
                await _synonym.Update(s => s.Project == project && s.Script == syn.Script && s.Index == syn.Index)
                    .Set(s => s.Delete, true)
                    .Execute();
            }*/

            // Saids
            var saids = await _saids.Query(s => s.Project == fromProject);
            foreach (var said in saids)
            {
                await _saids.Update(s => s.Project == project && s.Script == said.Script && s.Expression == said.Expression)
                    .Set(s => s.Patch, said.Patch)
                    .Set(s => s.Tests, said.Tests)
                    .Set(s => s.IsValid, said.IsValid)
                    .Execute();
            }

            // Suffixes
            /*var suffixes = await _suffixes.Query(s => s.Project == fromProject && s.IsTranslate);
            await _suffixes.Insert(suffixes.Select(s => new SuffixDocument
            {
                Project = project,
                IsTranslate = true,
                Input = s.Input,
                InClass = s.InClass,
                Output = s.Output,
                OutClass = s.OutClass,
            }));*/

            Console.WriteLine("Parser copying completed");
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

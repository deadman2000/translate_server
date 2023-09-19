using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Quartz;
using SCI_Lib.Resources;
using System;
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
        private readonly SynonymStore _synonyms;

        public InitJob(ILogger<InitJob> logger,
            UsersStore users,
            ProjectsStore projects,
            TranslateStore translate,
            YandexSpellcheck spellcheck,
            TextsStore texts,
            SCIService sci,
            SynonymStore synonyms)
        {
            _logger = logger;
            _users = users;
            _projects = projects;
            _translate = translate;
            _spellcheck = spellcheck;
            _texts = texts;
            _sci = sci;
            _synonyms = synonyms;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await UsersInit();
            //await EscapeStrings();
            await Spellchecking();
            await UpdateNullEngine();
            await SynonymDuplicates();
            _logger.LogInformation("Init complete");
        }

        private async Task SynonymDuplicates()
        {
            await _synonyms.Update(s => !s.Delete && s.WordA == s.WordB)
                .Set(s => s.Delete, true)
                .ExecuteMany();
        }

        private Task UpdateNullEngine()
        {
            return _projects.Update(p => p.Engine == null).Set(p => p.Engine, "sci").ExecuteMany();
        }

        private async Task EscapeStrings()
        {
            var projects = await _projects.All();
            foreach (var proj in projects)
                await Escape(proj.Code);
        }


        public async Task Escape(string project)
        {
            try
            {
                var package = _sci.Load(project);

                foreach (var res in package.GetResources<ResText>())
                    await Escape(project, res);
                foreach (var res in package.GetResources<ResScript>())
                    await Escape(project, res);
                foreach (var res in package.GetResources<ResMessage>())
                    await EscapeMsg(project, res);
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

                    if (res.Length > 0)
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

            _logger.LogInformation("Spell checking done");
        }
    }
}

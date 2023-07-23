using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Quartz;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
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

        public InitJob(ILogger<InitJob> logger, UsersStore users, ProjectsStore projects, TranslateStore translate, YandexSpellcheck spellcheck, TextsStore texts)
        {
            _logger = logger;
            _users = users;
            _projects = projects;
            _translate = translate;
            _spellcheck = spellcheck;
            _texts = texts;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await UsersInit();
            await Spellchecking();
            //await SpellFix();
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
        }

        private async Task Spellchecking()
        {
            // TODO Ignore untranslated
            /*_logger.LogInformation("Spell checking...");

            var projects = (await _projects.All()).Select(p => p.Code);
            foreach (var proj in projects)
            {
                var translates = (await _translate.Query(t => t.Project == proj && !t.Deleted && t.NextId == null && t.Spellcheck == null)).ToArray();
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

            _logger.LogInformation("Spell checking done");*/
        }

        private async Task SpellFix()
        {
            _logger.LogInformation("Spell fix...");

            var checks = _translate.Queryable().Where(t => !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Length > 0);
            foreach (var tr in checks)
            {
                var txt = await _texts.Get(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number);
                if (txt.Text == tr.Text)
                {
                    await _translate.Update(t => t.Id == tr.Id)
                        .Set(t => t.Spellcheck, Array.Empty<SpellResult>())
                        .Execute();
                }
            }

            _logger.LogInformation("Spell fix done");
        }
    }
}

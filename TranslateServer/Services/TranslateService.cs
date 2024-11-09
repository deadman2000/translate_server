using TranslateServer.Store;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Linq;
using System;
using TranslateServer.Model.Yandex;
using TranslateServer.Documents;
using System.Collections.Generic;

namespace TranslateServer.Services
{
    public class TranslateService
    {
        private readonly TranslateStore _translate;
        private readonly TextsStore _texts;
        private readonly VolumesStore _volumes;
        private readonly ProjectsStore _projects;
        private readonly SearchService _search;
        private readonly YandexSpellcheck _spellcheck;
        private readonly SpellcheckCache _spellcheckCache;

        public TranslateService(TranslateStore translate,
            TextsStore texts,
            VolumesStore volumes,
            ProjectsStore projects,
            SearchService search,
            YandexSpellcheck spellcheck,
            SpellcheckCache spellcheckCache)
        {
            _translate = translate;
            _texts = texts;
            _volumes = volumes;
            _projects = projects;
            _search = search;
            _spellcheck = spellcheck;
            _spellcheckCache = spellcheckCache;
        }

        public async Task UpdateVolumeTotal(string project, string volume)
        {
            var total = await _texts.Collection.Aggregate()
                .Match(t => t.Project == project && t.Volume == volume)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            await _volumes.Update(v => v.Project == project && v.Code == volume)
                .Set(v => v.Letters, total.Letters)
                .Set(v => v.Texts, total.Count)
                .Execute();
        }

        public async Task UpdateVolumeProgress(string project, string volume)
        {
            var translated = await _texts.Collection.Aggregate()
                .Match(t => t.Project == project && t.Volume == volume && t.HasTranslate)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            var approved = await _texts.Collection.Aggregate()
                .Match(t => t.Project == project && t.Volume == volume && t.TranslateApproved)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            await _volumes.Update(v => v.Project == project && v.Code == volume)
                .Set(v => v.TranslatedLetters, translated != null ? translated.Letters : 0)
                .Set(v => v.TranslatedTexts, translated != null ? translated.Count : 0)
                .Set(v => v.ApprovedLetters, approved != null ? approved.Letters : 0)
                .Set(v => v.ApprovedTexts, approved != null ? approved.Count : 0)
                .Execute();
        }

        public async Task UpdateProjectTotal(string project)
        {
            var res = await _volumes.Collection.Aggregate()
                .Match(t => t.Project == project)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Sum(t => t.Texts)
                })
                .FirstOrDefaultAsync();

            await _projects.Update(p => p.Code == project)
                .Set(p => p.Letters, res.Letters)
                .Set(p => p.Texts, res.Count)
                .Execute();
        }

        public async Task UpdateProjectProgress(string project)
        {
            var res = await _volumes.Collection.Aggregate()
                .Match(t => t.Project == project)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(v => v.TranslatedLetters),
                    Count = g.Sum(v => v.TranslatedTexts),
                    ALetters = g.Sum(v => v.ApprovedLetters),
                    ACount = g.Sum(v => v.ApprovedTexts),
                })
                .FirstOrDefaultAsync();

            await _projects.Update(p => p.Code == project)
                .Set(p => p.TranslatedLetters, res.Letters)
                .Set(p => p.TranslatedTexts, res.Count)
                .Set(p => p.ApprovedLetters, res.ALetters)
                .Set(p => p.ApprovedTexts, res.ACount)
                .Execute();
        }

        public async Task<TextTranslate> Submit(string project, string volume, int number, string text, string author, bool approveTransfer, string prevTranslateId = null)
        {
            var txt = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == number);
            if (txt == null)
                return null;

            //text = text.TrimEnd('\r', '\n');

            IEnumerable<SpellResult> spellcheck;
            if (text != txt.Text)
                spellcheck = await _spellcheck.Spellcheck(text);
            else
                spellcheck = Array.Empty<SpellResult>();

            TextTranslate translate = new()
            {
                Project = project,
                Volume = volume,
                Number = number,
                Text = text,
                Author = author,
                Editor = author,
                DateCreate = DateTime.UtcNow,
                Letters = txt.Letters,
                IsTranslate = txt.Text != text,
                Spellcheck = spellcheck,
            };

            if (prevTranslateId != null)
            {
                var prev = await _translate.GetById(prevTranslateId);
                if (prev != null)
                {
                    translate.Author = prev.Author;
                    translate.FirstId = prev.FirstId ?? prev.Id;
                }
            }

            await _translate.Insert(translate);

            if (prevTranslateId != null)
            {
                await _translate.Update(t => t.Id == prevTranslateId
                                          && t.NextId == null
                                          && !t.Deleted)
                    .Set(t => t.NextId, translate.Id)
                    .Execute();
            }

            bool needUpdate = false;
            if (!txt.HasTranslate)
            {
                await _texts.Update(t => t.Id == txt.Id).Set(t => t.HasTranslate, true).Execute();
                needUpdate = true;
            }

            if (txt.Text == text && !txt.TranslateApproved)
            {
                await _texts.Update(t => t.Id == txt.Id).Set(t => t.TranslateApproved, true).Execute();
                needUpdate = true;
            }
            else if (txt.TranslateApproved && !approveTransfer)
            {
                await _texts.Update(t => t.Id == txt.Id).Set(t => t.TranslateApproved, false).Execute();
                needUpdate = true;
            }

            if (needUpdate)
            {
                await UpdateVolumeProgress(project, volume);
                await UpdateProjectProgress(project);
            }

            await _volumes.Update(v => v.Project == project && v.Code == volume).Set(v => v.LastSubmit, DateTime.UtcNow).Execute();
            await _projects.Update(p => p.Code == project).Set(p => p.LastSubmit, DateTime.UtcNow).Execute();

            if (prevTranslateId != null)
                await _search.DeleteTranslate(prevTranslateId);
            await _search.IndexTranslate(translate);

            _spellcheckCache.ResetTotal(project);

            return translate;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Model;
using TranslateServer.Model.Import;
using TranslateServer.Store;

namespace TranslateServer.Services
{
    public class Importer
    {
        private readonly string _project;
        private readonly VolumesStore _volumes;
        private readonly SearchService _elastic;
        private readonly TextsStore _texts;
        private readonly TranslateStore _translate;

        private readonly HashSet<string> _dubl = new();
        private List<Volume> _volByNum;
        private readonly List<TextResource> _volTexts = new();
        private readonly List<TrMatch> _volMatches = new();
        readonly HashSet<string> _translated = new();


        public Importer(string project, VolumesStore volumes, SearchService elastic, TextsStore texts, TranslateStore translate)
        {
            _project = project;
            _volumes = volumes;
            _elastic = elastic;
            _texts = texts;
            _translate = translate;
        }

        public async Task Import(ImportBook book)
        {
            var regNum = new Regex(@"^(\d+)");
            var allVolumes = await _volumes.Query(v => v.Project == _project);
            //allVolumes = allVolumes.Where(v => v.Name.EndsWith("SCR")).ToList();

            await _translate.Delete(t => t.Project == _project);
            await _texts.Update(t => t.Project == _project)
                .Set(t => t.HasTranslate, false)
                .ExecuteMany();

            Dictionary<ImportVolume, int> volNums = new();

            foreach (var v in book.Volumes)
            //var v = book.Volumes.First();
            //var v = book.Volumes.First(v => v.Name.StartsWith("899"));
            {
                var numMatch = regNum.Match(v.Name);
                if (!numMatch.Success) continue;

                var num = int.Parse(numMatch.Groups[1].Value);
                volNums.Add(v, num);
            }

            foreach (var gr in volNums.GroupBy(kv => kv.Value))
            {
                Console.WriteLine($"Import resource {gr.Key}");
                var prefix = gr.Key + ".";
                var translates = gr.SelectMany(kv => kv.Key.Translates).ToList();

                _volByNum = allVolumes.Where(v => v.Name.StartsWith(prefix)).ToList();

                // Заполняем список всех текстов из всех волумов (производительность)
                _volTexts.Clear();
                foreach (var vol in _volByNum)
                {
                    var tx = await _texts.Query(t => t.Project == _project && t.Volume == vol.Code);
                    _volTexts.AddRange(tx);
                }

                _dubl.Clear();
                _volMatches.Clear();
                _translated.Clear();

                foreach (var tr in translates)
                //var tr = v.Translates.First(t => t.Src == "This icon is for walking.");
                {
                    await Process(tr);
                }

                await ProcessMatches();
            }
        }

        private async Task Process(ImportTranslate tr)
        {
            if (tr.Src.Contains("СКРИПТ")) return;
            if (_dubl.Contains(tr.Src)) return; // Скипаем повторы
            _dubl.Add(tr.Src);

            foreach (var vol in _volByNum)
            {
                var texts = _volTexts.Where(t => t.Text == tr.Src);
                if (texts.Any())
                {
                    foreach (var text in texts)
                        await Translate(text.Volume, text.Number, tr.Tr, "nota");
                    return;
                }
            }

            foreach (var vol in _volByNum)
            {
                var txtMatches = await _elastic.GetMatch(_project, vol.Code, tr.Src);
                foreach (var res in txtMatches)
                {
                    _volMatches.Add(new TrMatch(res, tr.Tr));
                }
            }
        }

        private async Task Translate(string volume, int number, string translate, string user)
        {
            var key = volume + ":" + number;
            if (_translated.Contains(key)) return;
            _translated.Add(key);

            await _translate.Insert(new TextTranslate()
            {
                Project = _project,
                Volume = volume,
                Number = number,
                Text = translate,
                Author = user,
                Editor = user,
                DateCreate = DateTime.UtcNow,
            });

            await _texts.Update(t => t.Project == _project && t.Volume == volume && t.Number == number)
                .Set(t => t.HasTranslate, true)
                .Execute();
        }

        private async Task ProcessMatches()
        {
            _volMatches.RemoveAll(m => _translated.Contains(m.Key));

            while (_volMatches.Any())
            {
                var best = _volMatches.OrderByDescending(m => m.Res.Score).First();
                await Translate(best.Res.Volume, best.Res.Number, best.Tr, "nota2");
                _volMatches.RemoveAll(m => m.Key == best.Key);
            }
        }



        class TrMatch
        {
            public TrMatch(MatchResult res, string tr)
            {
                Res = res;
                Tr = tr;
                Key = res.Volume + ":" + res.Number;
            }

            public MatchResult Res { get; }
            public string Tr { get; }
            public string Key { get; }
        }
    }
}

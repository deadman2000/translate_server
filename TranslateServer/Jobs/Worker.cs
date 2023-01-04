using Microsoft.Extensions.DependencyInjection;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts1_1;
using SCI_Lib.Resources.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Jobs
{
    public class Worker
    {
        private readonly TextsStore _texts;
        private readonly VolumesStore _volumes;
        private readonly SCIService _sci;
        private readonly ProjectsStore _projects;
        private readonly Project _project;
        private SCI_Lib.SCIPackage _package;

        public Worker(IServiceProvider serviceProvider, Project project)
        {
            using var scope = serviceProvider.CreateScope();
            _texts = scope.ServiceProvider.GetService<TextsStore>();
            _volumes = scope.ServiceProvider.GetService<VolumesStore>();
            _sci = scope.ServiceProvider.GetService<SCIService>();
            _projects = scope.ServiceProvider.GetService<ProjectsStore>();
            _project = project;
        }

        public async Task Extract()
        {
            Console.WriteLine($"Extract resources {_project.Code}");

            _package = _sci.Load(_project.Code);

            await Cleanup();

            foreach (var txt in _package.GetResources<ResText>())
            {
                var strings = txt.GetStrings();
                if (strings.Length == 0) continue;
                if (!strings.Any(s => !string.IsNullOrWhiteSpace(s))) continue;

                var volume = new Volume(_project, txt.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = strings[i];
                    if (!string.IsNullOrWhiteSpace(val))
                        await _texts.Insert(new TextResource(_project, volume, i, val));
                }
            }

            foreach (var scr in _package.GetResources<ResScript>())
            {
                var strings = scr.GetStrings();
                if (strings == null || strings.Length == 0) continue;
                if (!strings.Any(s => !string.IsNullOrWhiteSpace(s))) continue;

                var volume = new Volume(_project, scr.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = strings[i];
                    if (!string.IsNullOrWhiteSpace(val))
                        await _texts.Insert(new TextResource(_project, volume, i, val));
                }
            }


            foreach (var msg in _package.GetResources<ResMessage>())
            {
                var records = msg.GetMessages();
                if (records.Count == 0) continue;
                if (!records.Any(r => !string.IsNullOrWhiteSpace(r.Text))) continue;

                var volume = new Volume(_project, msg.FileName);
                await _volumes.Insert(volume);

                /*Dictionary<ushort, IEnumerable<Object1_1>> nounObjects = null;
                if (package.GetResource<ResVocab>(997) != null)
                {
                    var scrRes = package.GetResource<ResScript>(msg.Number);
                    if (scrRes != null)
                    {
                        nounObjects = new();
                        var scr = scrRes.GetScript() as Script1_1;

                        nounObjects = scr.Objects.Where(o => o.HasProperty("noun"))
                            .Select(o => new { o, n = o["noun"] })
                            .Where(p => p.n != 0)
                            .GroupBy(p => p.n, p => p.o)
                            .ToDictionary(g => g.Key, g => (IEnumerable<Object1_1>)g.ToList());
                    }
                }*/

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    if (string.IsNullOrWhiteSpace(r.Text)) continue;

                    /*List<string> noun = null;
                    if (nounObjects != null && nounObjects.TryGetValue(r.Noun, out var objects))
                    {
                        noun = new List<string>();
                        foreach (var o in objects)
                        {
                            var link = ExtractImage(o);
                            if (!String.IsNullOrEmpty(link))
                                noun.Add(link);
                        }
                    }*/

                    await _texts.Insert(new TextResource(_project, volume, i, r.Text)
                    {
                        Talker = r.Talker,
                        Verb = r.Verb,
                        //Noun = noun
                    });
                }
            }

            await _volumes.RecalcLetters(_project.Code, _texts);
            await _projects.RecalcLetters(_project.Code, _volumes);
        }

        private async Task Cleanup()
        {
            await _texts.Delete(r => r.Project == _project.Code);
            await _volumes.Delete(v => v.Project == _project.Code);
        }

        private string ExtractImage(Object1_1 obj)
        {
            var package = obj.Script.Package;

            if (obj.TryGetProperty("view", out var viewNum))
            {
                var resView = package.GetResource<ResView>(viewNum);
                if (resView == null) return null;
                var view = ExtractView(resView);

                if (!obj.TryGetProperty("loop", out var loop)) loop = 0;
                if (!obj.TryGetProperty("cel", out var cel)) cel = 0;

                if (loop >= view.Loops.Count) loop = (ushort)(view.Loops.Count - 1);
                if (cel >= view.Loops[loop].Cells.Count) cel = (ushort)(view.Loops[loop].Cells.Count - 1);

                return $"resources/{_project.Code}/views/{viewNum}.{loop}.{cel}.png";
            }

            return null;
        }

        private readonly Dictionary<ushort, SCIView> _extractedViews = new();

        private SCIView ExtractView(ResView resView)
        {
            var dir = $"resources/{_project.Code}/views";
            if (_extractedViews.Count == 0)
                Directory.CreateDirectory(dir);

            if (_extractedViews.TryGetValue(resView.Number, out var view)) return view;

            view = resView.GetView();
            _extractedViews.Add(resView.Number, view);

            if (File.Exists(Path.Combine(dir, $"{resView.Number}.0.0.png"))) return view;

            for (int l = 0; l < view.Loops.Count; l++)
            {
                var loop = view.Loops[l];

                for (int c = 0; c < loop.Cells.Count; c++)
                {
                    var cell = loop.Cells[c];
                    cell.GetImage().Save(Path.Combine(dir, $"{resView.Number}.{l}.{c}.png"));
                }
            }

            return view;
        }

    }
}

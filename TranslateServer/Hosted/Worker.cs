using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
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

namespace TranslateServer.Hosted
{
    public class Worker
    {
        private readonly TextsService texts;
        private readonly VolumesService volumes;
        private readonly SCIService sci;
        private readonly ProjectsService projects;
        private readonly Project project;
        private SCI_Lib.SCIPackage package;

        public Worker(IServiceProvider serviceProvider, Project project)
        {
            using var scope = serviceProvider.CreateScope();
            texts = scope.ServiceProvider.GetService<TextsService>();
            volumes = scope.ServiceProvider.GetService<VolumesService>();
            sci = scope.ServiceProvider.GetService<SCIService>();
            projects = scope.ServiceProvider.GetService<ProjectsService>();
            this.project = project;
        }

        public async Task Extract()
        {
            Console.WriteLine($"Extract resources {project.Code}");

            package = sci.Load(project.Code);

            await Cleanup();

            foreach (var txt in package.GetResources<ResText>())
            {
                var strings = txt.GetStrings();
                if (strings.Length == 0) continue;

                var volume = new Volume(project, txt.FileName);
                await volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    await texts.Insert(new TextResource(project, volume, i, strings[i]));
                }
            }

            foreach (var scr in package.GetResources<ResScript>())
            {
                var strings = scr.GetStrings();
                if (strings == null || strings.Length == 0) continue;

                var volume = new Volume(project, scr.FileName);
                await volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    await texts.Insert(new TextResource(project, volume, i, strings[i]));
                }
            }


            foreach (var msg in package.GetResources<ResMessage>())
            {
                var records = msg.GetMessages();
                if (records.Count == 0) continue;

                var volume = new Volume(project, msg.FileName);
                await volumes.Insert(volume);

                Dictionary<ushort, IEnumerable<Object1_1>> nounObjects = null;
                if (package.GetResource<ResVocab>(997) != null)
                {
                    var scrRes = package.GetResource<ResScript>(msg.Number);
                    if (scrRes != null)
                    {
                        nounObjects = new();
                        var scr = scrRes.GetScript() as Script1_1;

                        nounObjects = scr.Objects.Where(o => o.HasProperty("noun"))
                            .Select(o => new { o, n = o["noun"] })
                            .GroupBy(p => p.n, p => p.o)
                            .ToDictionary(g => g.Key, g => (IEnumerable<Object1_1>)g.ToList());
                    }
                }

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];

                    List<string> noun = null;
                    if (nounObjects != null && nounObjects.TryGetValue(r.Noun, out var objects))
                    {
                        noun = new List<string>();
                        foreach (var o in objects)
                        {
                            var link = ExtractImage(o);
                            if (!String.IsNullOrEmpty(link))
                                noun.Add(link);
                        }
                    }

                    await texts.Insert(new TextResource(project, volume, i, r.Text)
                    {
                        Talker = r.Talker,
                        Verb = r.Verb,
                        Noun = noun
                    });
                }
            }

            var volList = await volumes.Query(v => v.Project == project.Code);
            foreach (var vol in volList)
            {
                var res = await texts.Collection.Aggregate()
                    .Match(t => t.Project == project.Code && t.Volume == vol.Code)
                    .Group(t => t.Volume,
                    g => new
                    {
                        Total = g.Sum(t => t.Letters),
                        Count = g.Count()
                    })
                    .FirstOrDefaultAsync();

                await volumes.Update(v => v.Id == vol.Id)
                    .Set(v => v.Letters, res.Total)
                    .Set(v => v.Texts, res.Count)
                    .Execute();
            }

            {
                var res = await volumes.Collection.Aggregate()
                    .Match(v => v.Project == project.Code)
                    .Group(v => v.Project,
                    g => new
                    {
                        Total = g.Sum(t => t.Letters),
                        Count = g.Sum(t => t.Texts)
                    })
                    .FirstOrDefaultAsync();

                await projects.Update(p => p.Id == project.Id)
                    .Set(p => p.Letters, res.Total)
                    .Set(p => p.Texts, res.Count)
                    .Execute();
            }
        }

        private async Task Cleanup()
        {
            await texts.Delete(r => r.Project == project.Code);
            await volumes.Delete(v => v.Project == project.Code);
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

                return $"resources/{project.Code}/views/{viewNum}.{loop}.{cel}.png";
            }

            return null;
        }

        private readonly Dictionary<ushort, SCIView> _extractedViews = new();

        private SCIView ExtractView(ResView resView)
        {
            var dir = $"resources/{project.Code}/views";
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

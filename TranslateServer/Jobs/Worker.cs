using Microsoft.Extensions.DependencyInjection;
using SCI_Lib.Resources;
using System;
using System.Collections.Generic;
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
        private readonly TranslateStore _translates;
        private readonly Project _project;
        private SCI_Lib.SCIPackage _package;

        public Worker(IServiceProvider serviceProvider, Project project)
        {
            using var scope = serviceProvider.CreateScope();
            _texts = scope.ServiceProvider.GetService<TextsStore>();
            _volumes = scope.ServiceProvider.GetService<VolumesStore>();
            _sci = scope.ServiceProvider.GetService<SCIService>();
            _translates = scope.ServiceProvider.GetService<TranslateStore>();
            _project = project;
        }

        public async Task Extract()
        {
            _package = _sci.Load(_project.Code);
            var now = DateTime.UtcNow;

            await Cleanup();

            HashSet<string> volumesHash = new();
            foreach (var txt in _package.GetResources<ResText>())
            {
                var strings = txt.GetStrings();
                if (strings.Length == 0) continue;
                if (!strings.Any(s => !string.IsNullOrWhiteSpace(s))) continue;
                if (volumesHash.Contains(txt.FileName)) continue;
                Console.WriteLine(txt.FileName);

                volumesHash.Add(txt.FileName);
                var volume = new Volume(_project, txt.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = strings[i];
                    if (!string.IsNullOrWhiteSpace(val))
                        await _texts.Insert(new TextResource(_project, volume, i, val));
                }
            }

            volumesHash.Clear();
            foreach (var scr in _package.GetResources<ResScript>())
            {
                var strings = scr.GetStrings();
                if (strings == null || strings.Length == 0) continue;
                if (!strings.Any(s => !string.IsNullOrWhiteSpace(s))) continue;
                if (volumesHash.Contains(scr.FileName)) continue;
                Console.WriteLine(scr.FileName);

                volumesHash.Add(scr.FileName);
                var volume = new Volume(_project, scr.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = strings[i];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var txt = new TextResource(_project, volume, i, val)
                        {
                            HasTranslate = true,
                            TranslateApproved = true
                        };
                        await _texts.Insert(txt);

                        await _translates.Insert(new TextTranslate
                        {
                            Text = txt.Text,
                            Letters = txt.Letters,
                            Project = _project.Code,
                            Volume = volume.Code,
                            Number = i,
                            Author = "system",
                            Editor = "system",
                            DateCreate = now,
                        });
                    }
                }
            }

            volumesHash.Clear();
            foreach (var msg in _package.GetResources<ResMessage>())
            {
                var records = msg.GetMessages();
                if (records.Count == 0) continue;
                if (!records.Any(r => !string.IsNullOrWhiteSpace(r.Text))) continue;
                if (volumesHash.Contains(msg.FileName)) continue;
                Console.WriteLine(msg.FileName);

                volumesHash.Add(msg.FileName);
                var volume = new Volume(_project, msg.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    if (string.IsNullOrWhiteSpace(r.Text)) continue;
                    await _texts.Insert(new TextResource(_project, volume, i, r.Text));
                }
            }
        }

        private async Task Cleanup()
        {
            await _texts.Delete(r => r.Project == _project.Code);
            await _volumes.Delete(v => v.Project == _project.Code);
            await _translates.Delete(r => r.Project == _project.Code);
        }
    }
}

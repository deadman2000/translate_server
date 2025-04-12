using AGSUnpacker.Lib.Translation;
using Microsoft.Extensions.DependencyInjection;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Elements;
using SCI_Lib.Resources.Scripts.Sections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Tools
{
    public class Extractor
    {
        private readonly TextsStore _texts;
        private readonly VolumesStore _volumes;
        private readonly SCIService _sci;
        private readonly TranslateStore _translates;
        private readonly Project _project;
        private SCI_Lib.SCIPackage _package;
        private HashSet<string> _filesFilter;

        public Extractor(IServiceProvider serviceProvider, Project project)
        {
            using var scope = serviceProvider.CreateScope();
            _texts = scope.ServiceProvider.GetService<TextsStore>();
            _volumes = scope.ServiceProvider.GetService<VolumesStore>();
            _sci = scope.ServiceProvider.GetService<SCIService>();
            _translates = scope.ServiceProvider.GetService<TranslateStore>();
            _project = project;
        }

        public async Task CleanAndExtractAll()
        {
            await Cleanup();

            if (_project.Engine == "ags")
                await ExtractAGS();
            else
                await ExtractSCI();
        }


        public async Task ExtractFiles(params string[] files)
        {
            _filesFilter = files.Select(f=>f.ToUpper()).ToHashSet();

            if (_project.Engine == "ags")
                await ExtractAGS();
            else
                await ExtractSCI();
        }


        public async Task ExtractSCI()
        {
            _package = _sci.Load(_project);
            var enc = _package.GameEncoding;
            var now = DateTime.UtcNow;

            HashSet<string> volumesHash = new();
            foreach (var txt in _package.GetResources<ResText>())
            {
                var strings = txt.GetStrings();
                if (strings.Length == 0) continue;
                if (!strings.Any(s => !string.IsNullOrWhiteSpace(s))) continue;
                if (volumesHash.Contains(txt.FileName)) continue;
                if (!IsFilePass(txt.FileName)) continue;
                Console.WriteLine(txt.FileName);

                volumesHash.Add(txt.FileName);
                var volume = new Volume(_project, txt.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = enc.EscapeString(strings[i]);
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
                if (!IsFilePass(scr.FileName)) continue;
                Console.WriteLine(scr.FileName);

                volumesHash.Add(scr.FileName);
                var volume = new Volume(_project, scr.FileName);
                await _volumes.Insert(volume);

                HashSet<string> needTranslate = new();

                if (scr.GetScript() is Script script)
                {
                    foreach (var sec in script.Get<ClassSection>())
                    {
                        sec.Prepare();
                        foreach (var prop in sec.Properties)
                        {
                            if (prop.Name != "name" && prop.Reference is StringConst sc)
                                needTranslate.Add(sc.Value);
                        }
                    }

                    foreach (var s in script.AllStrings())
                    {
                        if (s.XRefs.Count > 0)
                            needTranslate.Add(s.Value);
                    }
                }

                for (int i = 0; i < strings.Length; i++)
                {
                    var val = enc.EscapeString(strings[i]);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var txt = new TextResource(_project, volume, i, val);

                        if (needTranslate.Contains(val))
                        {
                            await _texts.Insert(txt);
                        }
                        else
                        {
                            txt.HasTranslate = true;
                            txt.TranslateApproved = true;
                            await _texts.Insert(txt);

                            await _translates.Insert(new TextTranslate
                            {
                                Text = txt.Text,
                                Letters = txt.Letters,
                                Project = _project.Code,
                                Volume = volume.Code,
                                Number = i,
                                IsTranslate = false,
                                Author = "system",
                                Editor = "system",
                                DateCreate = now,
                            });
                        }
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
                if (!IsFilePass(msg.FileName)) continue;
                Console.WriteLine(msg.FileName);

                volumesHash.Add(msg.FileName);
                var volume = new Volume(_project, msg.FileName);
                await _volumes.Insert(volume);

                for (int i = 0; i < records.Count; i++)
                {
                    var val = enc.EscapeString(records[i].Text);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    await _texts.Insert(new TextResource(_project, volume, i, val));
                }
            }
        }

        private bool IsFilePass(string fileName)
        {
            if (_filesFilter == null) return true;

            fileName = Path.GetFileName(fileName);
            return _filesFilter.Contains(fileName.ToUpper());
        }

        private async Task ExtractAGS()
        {
            await ExtractTRA();
            await ExtractTRS();
        }

        private async Task ExtractTRA()
        {
            var path = _sci.GetProjectPath(_project.Code);
            var traFiles = Directory.GetFiles(path, "*.tra", SearchOption.AllDirectories);

            foreach (var filePath in traFiles)
            {
                if (!IsFilePass(filePath)) continue;

                AGSTranslation translation = new();
                translation.Decompile(filePath);

                var dir = Path.GetDirectoryName(filePath);
                var dirInfo = new DirectoryInfo(dir);

                var volume = new Volume(_project, dirInfo.Name);
                await _volumes.Insert(volume);

                for (int i = 0; i < translation.OriginalLines.Count; i++)
                {
                    var en = translation.TranslatedLines[i].Replace("[", "\n");
                    var fr = translation.OriginalLines[i].Replace("[", "\n");
                    await _texts.Insert(new TextResource(_project, volume, i, en)
                    {
                        Description = fr
                    });
                }
            }
        }

        private async Task ExtractTRS()
        {
            var path = _sci.GetProjectPath(_project.Code);
            var trsFiles = Directory.GetFiles(path, "*.trs", SearchOption.AllDirectories);

            foreach (var filePath in trsFiles)
            {
                AGSTranslation translation = AGSTranslation.ReadSourceFile(filePath);

                var name = Path.GetFileName(filePath);
                var volume = new Volume(_project, name);
                await _volumes.Insert(volume);

                for (int i = 0; i < translation.OriginalLines.Count; i++)
                {
                    var en = translation.OriginalLines[i].Replace("[", "\n");
                    await _texts.Insert(new TextResource(_project, volume, i, en));
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

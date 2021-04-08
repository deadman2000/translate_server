using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.Threading;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using System.Linq;

namespace TranslateServer.Hosted
{
    public class ResourceExtractor : IHostedService
    {
        private readonly ServerConfig _config;
        private readonly IServiceProvider _serviceProvider;

        public ResourceExtractor(IOptions<ServerConfig> opConfig, IServiceProvider serviceProvider)
        {
            _config = opConfig.Value;
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Work();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task Work()
        {
            while (true)
            {
                try
                {
                    await Process();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                await Task.Delay(5000);
            }
        }

        private async Task Process()
        {
            var scope = _serviceProvider.CreateScope();
            var projects = scope.ServiceProvider.GetService<ProjectsService>();

            var toProcess = await projects.Query(p => p.Status == ProjectStatus.Processing);
            foreach (var project in toProcess)
            {
                try
                {
                    Console.WriteLine($"Extract resources {project.ShortName}");
                    await Process(projects, project);

                    await projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Working)
                        .Execute();

                    Console.WriteLine($"Project {project.ShortName} resources extracted");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Error)
                        .Set(p => p.Error, ex.ToString())
                        .Execute();
                }
            }
        }

        private async Task Process(ProjectsService projects, Project project)
        {
            var gameDir = $"{_config.ProjectsDir}/{project.ShortName}/";
            var package = SCIPackage.Load(gameDir);

            var scope = _serviceProvider.CreateScope();
            var resources = scope.ServiceProvider.GetService<ResourcesService>();
            var volumes = scope.ServiceProvider.GetService<VolumesService>();

            await resources.Delete(r => r.Project == project.ShortName);
            await volumes.Delete(v => v.Project == project.ShortName);

            foreach (var txt in package.GetResouces<ResText>())
            {
                var strings = txt.GetStrings();
                if (strings.Length == 0) continue;

                await volumes.Insert(new Volume
                {
                    Project = project.ShortName,
                    Name = txt.FileName
                });

                for (int i = 0; i < strings.Length; i++)
                {
                    await resources.Insert(new TextResource(project, txt.FileName, i, strings[i]));
                }
            }

            foreach (var scr in package.GetResouces<ResScript>())
            {
                var strings = scr.GetStrings();
                if (strings == null || strings.Length == 0) continue;

                await volumes.Insert(new Volume
                {
                    Project = project.ShortName,
                    Name = scr.FileName
                });

                for (int i = 0; i < strings.Length; i++)
                {
                    await resources.Insert(new TextResource(project, scr.FileName, i, strings[i]));
                }
            }


            foreach (var msg in package.GetResouces<ResMessage>())
            {
                var records = msg.GetMessages();
                if (records.Count == 0) continue;

                await volumes.Insert(new Volume
                {
                    Project = project.ShortName,
                    Name = msg.FileName
                });

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    await resources.Insert(new TextResource(project, msg.FileName, i, r.Text, r.Talker));
                }
            }

            var volList = await volumes.Query(v => v.Project == project.ShortName);
            foreach (var vol in volList)
            {
                var res = await resources.Collection.Aggregate()
                    .Match(t => t.Project == project.ShortName && t.Volume == vol.Name)
                    .Group(t => t.Volume,
                    g => new
                    {
                        Total = g.Sum(t => t.NumberOfLetters)
                    })
                    .FirstOrDefaultAsync();

                await volumes.Update(v => v.Id == vol.Id)
                    .Set(v => v.NumberOfLetters, res.Total)
                    .Execute();
            }

            {
                var res = await volumes.Collection.Aggregate()
                    .Match(v => v.Project == project.ShortName)
                    .Group(v => v.Project,
                    g => new
                    {
                        Total = g.Sum(t => t.NumberOfLetters)
                    })
                    .FirstOrDefaultAsync();

                await projects.Update(p => p.Id == project.Id)
                    .Set(p => p.NumberOfLetters, res.Total)
                    .Execute();
            }
        }

    }
}

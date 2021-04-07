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
                    await Process(project);
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
                        .Execute();
                }
            }
        }

        private async Task Process(Project project)
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
                    await resources.Insert(new TextResource
                    {
                        Project = project.ShortName,
                        Volume = txt.FileName,
                        Number = i,
                        Text = strings[i],
                    });
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
                    await resources.Insert(new TextResource
                    {
                        Project = project.ShortName,
                        Volume = scr.FileName,
                        Number = i,
                        Text = strings[i],
                    });
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
                    await resources.Insert(new TextResource
                    {
                        Project = project.ShortName,
                        Volume = msg.FileName,
                        Number = i,
                        Text = r.Text,
                        Talker = r.Talker
                    });
                }
            }
        }
    }
}

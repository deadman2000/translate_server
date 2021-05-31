﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using SCI_Lib.Resources;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Hosted
{
    public class ResourceExtractor : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public ResourceExtractor(IServiceProvider serviceProvider)
        {
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
            using var scope = _serviceProvider.CreateScope();
            var projects = scope.ServiceProvider.GetService<ProjectsService>();

            var toProcess = await projects.Query(p => p.Status == ProjectStatus.Processing);
            foreach (var project in toProcess)
            {
                try
                {
                    await Extract(project);

                    await projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Working)
                        .Execute();

                    await CreateIndex(project);

                    Console.WriteLine($"Project {project.Code} resources extracted");
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

        private async Task Extract(Project project)
        {
            Console.WriteLine($"Extract resources {project.Code}");

            using var scope = _serviceProvider.CreateScope();
            var texts = scope.ServiceProvider.GetService<TextsService>();
            var volumes = scope.ServiceProvider.GetService<VolumesService>();
            var sci = scope.ServiceProvider.GetService<SCIService>();

            var package = sci.Load(project.Code);

            await texts.Delete(r => r.Project == project.Code);
            await volumes.Delete(v => v.Project == project.Code);

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

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    await texts.Insert(new TextResource(project, volume, i, r.Text, r.Talker));
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

                var projects = scope.ServiceProvider.GetService<ProjectsService>();
                await projects.Update(p => p.Id == project.Id)
                    .Set(p => p.Letters, res.Total)
                    .Set(p => p.Texts, res.Count)
                    .Execute();
            }
        }

        private async Task CreateIndex(Project project)
        {
            Console.WriteLine("Indexing...");

            using var scope = _serviceProvider.CreateScope();
            var texts = scope.ServiceProvider.GetService<TextsService>();
            var elastic = scope.ServiceProvider.GetService<SearchService>();

            var items = await texts.Query(t => t.Project == project.Code);

            await elastic.DeleteProject(project.Code);
            await elastic.InsertTexts(items.ToList());
        }

    }
}

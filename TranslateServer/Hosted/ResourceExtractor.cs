using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts1_1;
using SCI_Lib.Resources.View;
using System;
using System.Collections.Generic;
using System.IO;
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
                    Worker worker = new Worker(_serviceProvider, project);
                    await worker.Extract();

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

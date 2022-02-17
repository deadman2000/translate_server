using MongoDB.Driver;
using Quartz;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Jobs
{
    public class ResourceExtractor : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ProjectsService _projects;
        private readonly TextsService _texts;
        private readonly SearchService _search;

        public static void Schedule(IServiceCollectionQuartzConfigurator q)
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UseDefaultThreadPool(x => { x.MaxConcurrency = 1; });
            q.ScheduleJob<ResourceExtractor>(j => j
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(1)
                    .RepeatForever())
            );
        }

        public ResourceExtractor(IServiceProvider serviceProvider, ProjectsService projects, TextsService texts, SearchService search)
        {
            _serviceProvider = serviceProvider;
            _projects = projects;
            _texts = texts;
            _search = search;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var toProcess = await _projects.Query(p => p.Status == ProjectStatus.Processing);
            foreach (var project in toProcess)
            {
                Console.WriteLine($"Extracting resources for {project.Code}");
                try
                {
                    Worker worker = new(_serviceProvider, project);
                    await worker.Extract();

                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Working)
                        .Execute();

                    await CreateIndex(project);

                    Console.WriteLine($"Project {project.Code} resources extracted");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Error)
                        .Set(p => p.Error, ex.ToString())
                        .Execute();
                }
            }
        }

        private async Task CreateIndex(Project project)
        {
            Console.WriteLine("Indexing...");

            var items = await _texts.Query(t => t.Project == project.Code);

            await _search.DeleteProject(project.Code);
            await _search.InsertTexts(items.ToList());
        }
    }
}

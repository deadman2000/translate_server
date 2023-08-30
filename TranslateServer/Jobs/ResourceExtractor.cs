using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Quartz;
using SCI_Lib.Analyzer;
using SCI_Lib.Resources.Scripts.Elements;
using SCI_Lib.SCI0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Jobs
{
    class ResourceExtractor : IJob
    {
        private readonly ILogger<ResourceExtractor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ProjectsStore _projects;
        private readonly VolumesStore _volumes;
        private readonly TextsStore _texts;
        private readonly SearchService _search;
        private readonly TranslateService _translateService;
        private readonly SCIService _sci;

        public static void Schedule(IServiceCollectionQuartzConfigurator q)
        {
            q.ScheduleJob<ResourceExtractor>(j => j
                .StartAt(DateBuilder.FutureDate(10, IntervalUnit.Second))
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(1)
                    .RepeatForever())
            );
        }

        public ResourceExtractor(
            ILogger<ResourceExtractor> logger,
            IServiceProvider serviceProvider,
            ProjectsStore projects,
            VolumesStore volumes,
            TextsStore texts,
            SearchService search,
            TranslateService translateService,
            SCIService sci
        )
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _projects = projects;
            _volumes = volumes;
            _texts = texts;
            _search = search;
            _translateService = translateService;
            _sci = sci;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await TextExtract();
            await ResExtract();
        }

        private async Task TextExtract()
        {
            var toProcess = await _projects.Query(p => p.Status == ProjectStatus.TextExtract);
            foreach (var project in toProcess)
            {
                _logger.LogInformation($"Extracting text for {project.Code}");
                try
                {
                    Worker worker = new(_serviceProvider, project);
                    await worker.Extract();

                    _logger.LogInformation($"Project {project.Code} text extracted");
                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.ResourceExtract)
                        .Execute();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{project.Code} Text extract error");
                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Error)
                        .Set(p => p.Error, ex.ToString())
                        .Execute();
                }
            }
        }

        private async Task ResExtract()
        {
            var toProcess = await _projects.Query(p => p.Status == ProjectStatus.ResourceExtract);
            foreach (var project in toProcess)
            {
                _logger.LogInformation($"Extracting resources for {project.Code}");
                try
                {
                    await CreateIndex(project);

                    if (project.Engine == "sci")
                        await PrintUsage(project);

                    _logger.LogInformation($"Project {project.Code} resources extracted");
                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Ready)
                        .Execute();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{project.Code} Res extract error");
                    await _projects.Update(p => p.Id == project.Id)
                        .Set(p => p.Status, ProjectStatus.Error)
                        .Set(p => p.Error, ex.ToString())
                        .Execute();
                }
            }
        }

        private async Task PrintUsage(Project project)
        {
            try
            {
                var package = _sci.Load(project.Code);
                if (package is not SCI0Package) return;

                var search = new TextUsageSearch(package);
                var result = search.FindUsage();
                foreach (var p in result)
                {
                    IEnumerable<SaidExpression> saids = p.Saids;
                    foreach (var said in saids)
                        said.Normalize();
                    saids = saids.Where(s => s.Label != "kiss/angel>"); // PQ2

                    var volume = $"text_{p.Txt:D3}";
                    var descr = string.Join('\n', saids.Select(s => s.Label));

                    await _texts.Update(t => t.Project == project.Code && t.Volume == volume && t.Number == p.Index)
                        .Set(t => t.Description, descr)
                        .Execute();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{project.Code} Extract print said error");
            }
        }

        private async Task CreateIndex(Project pr)
        {
            var project = pr.Code;
            _logger.LogInformation($"Indexing {project}");

            await _volumes.RecalcLetters(project, _texts);
            await _projects.RecalcLetters(project, _volumes);

            var volumes = await _volumes.Query(v => v.Project == project);
            foreach (var vol in volumes)
            {
                await _translateService.UpdateVolumeTotal(project, vol.Code);
                await _translateService.UpdateVolumeProgress(project, vol.Code);
            }
            await _translateService.UpdateProjectTotal(project);
            await _translateService.UpdateProjectProgress(project);

            var items = await _texts.Query(t => t.Project == project);
            await _search.DeleteProject(project);
            await _search.InsertTexts(items.ToList());
        }
    }
}

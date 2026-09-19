using System.Net;
using System.Text;
using Flurl.Http.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Quartz;
using TranslateServer.Documents;
using TranslateServer.Services;
using Xunit;

namespace TranslateServer.Tests;

public class MigrationTests
{
    private static IConfiguration Configuration(bool disableJobs = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DisableJobs"] = disableJobs.ToString(),
            ["ConnectionStrings:Mongo"] = "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=100",
            ["ConnectionStrings:Elastic"] = "http://127.0.0.1:9200",
            ["Mcp:Enabled"] = "true",
            ["Mcp:Jwt:Enabled"] = "false",
            ["Mcp:Agents:0:Token"] = "migration-test-token",
            ["Mcp:Agents:0:AgentName"] = "MigrationTest",
            ["Mcp:Agents:0:ReadOnly"] = "true",
            ["Mcp:Agents:0:Enabled"] = "true"
        }).Build();

    private static Task<IHost> CreateHost() => new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer()
            .UseEnvironment("Production")
            .UseConfiguration(Configuration())
            .UseStartup<Startup>())
        .StartAsync();

    [Fact]
    public async Task HealthEndpointStartsWithoutExternalServices()
    {
        using var host = await CreateHost();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/api/users/me")]
    public async Task ProtectedEndpointsRejectAnonymousRequests(string path)
    {
        using var host = await CreateHost();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpToolKeepsAuthenticatedRequestContext()
    {
        using var host = await CreateHost();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-MCP-Token", "migration-test-token");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-03-26");
        using var response = await client.PostAsync("/mcp", new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_status","arguments":{}}}""",
            Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("MigrationTest", body);
        Assert.Contains("readOnly", body);
        Assert.DoesNotContain("\"isError\":true", body);
    }

    [Fact]
    public void MongoLinqTranslatesFiltersAndDistinctProjection()
    {
        using var client = new MongoClient("mongodb://127.0.0.1:27017");
        var comments = client.GetDatabase("MigrationTests").GetCollection<Comment>("Comments");
        var query = comments.AsQueryable()
            .Where(comment => comment.Project == "test" && comment.Volume == "chapter")
            .Select(comment => comment.Author)
            .Distinct();
        var pipeline = query.ToString();
        Assert.Contains("$match", pipeline);
        Assert.Contains("$group", pipeline);
        Assert.DoesNotContain("not supported", pipeline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuartzRegistersExistingJobsAndIntervals()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(disableJobs: false);
        services.AddSingleton(configuration);
        services.AddLogging();
        new Startup(configuration).ConfigureServices(services);
        await using var provider = services.BuildServiceProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try
        {
            Assert.Equal("Scheduler-Core", scheduler.SchedulerInstanceId);
            var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            Assert.Equal(3, jobs.Count);
            var intervals = new List<TimeSpan>();
            foreach (var job in jobs)
            {
                var triggers = await scheduler.GetTriggersOfJob(job);
                intervals.AddRange(triggers.OfType<ISimpleTrigger>()
                    .Where(trigger => trigger.RepeatCount == -1)
                    .Select(trigger => trigger.RepeatInterval));
            }
            Assert.Equal(2, intervals.Count);
            Assert.All(intervals, interval => Assert.Equal(TimeSpan.FromMinutes(1), interval));
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }

    [Fact]
    public async Task FlurlReadsLowercaseTranslationJson()
    {
        using var http = new HttpTest();
        http.RespondWith("""{"translations":[{"text":"Привет"}]}""");
        var service = new YandexTranslateService(Configuration());
        var result = await service.Translate(["Hello"], "en");
        Assert.Equal("Привет", Assert.Single(result));
    }

    [Fact]
    public async Task FlurlReadsAndFiltersSpellcheckJson()
    {
        using var http = new HttpTest();
        http.RespondWith("""[{"code":1,"pos":0,"row":0,"col":0,"len":6,"word":"превет","s":["превет","привет"]}]""");
        var result = Assert.Single(await new YandexSpellcheck().Spellcheck("превет"));
        Assert.Equal("привет", Assert.Single(result.S));
        Assert.Equal(6, result.Len);
    }
}

using MongoDB.Driver;
using Quartz;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Store;

namespace TranslateServer.Jobs
{
    class InitJob : IJob
    {
        public static void Schedule(IServiceCollectionQuartzConfigurator q)
        {
            q.ScheduleJob<InitJob>(j => j.StartNow());
        }

        private readonly UsersStore _users;

        public InitJob(UsersStore users)
        {
            _users = users;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await UsersInit();
        }

        private async Task UsersInit()
        {
            var cnt = await _users.Collection.CountDocumentsAsync(u => true);
            if (cnt > 0)
                return;

            var user = new UserDocument
            {
                Login = "admin",
                Role = UserDocument.ADMIN
            };
            user.SetPassword("admin");
            await _users.Insert(user);
        }
    }
}

using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class VolumesStore : MongoBaseService<Volume>
    {
        public VolumesStore(MongoService mongo) : base(mongo, "Volumes")
        {
            var indexKeysDefinition = Builders<Volume>.IndexKeys.Ascending(v => v.Project);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<Volume>(indexKeysDefinition));
        }

        public async Task RecalcLetters(string project, TextsStore texts)
        {
            var volList = await Query(v => v.Project == project);
            foreach (var vol in volList)
            {
                var res = await texts.Collection.Aggregate()
                    .Match(t => t.Project == project && t.Volume == vol.Code)
                    .Group(t => t.Volume,
                    g => new
                    {
                        Total = g.Sum(t => t.Letters),
                        Count = g.Count()
                    })
                    .FirstOrDefaultAsync();

                if (res != null)
                {
                    await Update(v => v.Id == vol.Id)
                        .Set(v => v.Letters, res.Total)
                        .Set(v => v.Texts, res.Count)
                        .Execute();
                }
            }
        }
    }
}

using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class VideoReferenceStore : MongoBaseService<VideoReference>
    {
        public VideoReferenceStore(MongoService mongo) : base(mongo, "VideoReferences")
        {
        }

        public async Task<VideoReference> Create(string project, string volume, int number, string videoId)
        {
            var result = await Update()
                .Where(r => r.Project == project && r.Volume == volume && r.Number == number && r.VideoId == videoId)
                .Set(r => r.Project, project)
                .Set(r => r.Volume, volume)
                .Set(r => r.Number, number)
                .Set(r => r.VideoId, videoId)
                .Upsert();

            if (result.UpsertedId != null)
                return await GetById(result.UpsertedId.ToString());
            return await Get(r => r.Project == project && r.Volume == volume && r.Number == number && r.VideoId == videoId);
        }
    }
}

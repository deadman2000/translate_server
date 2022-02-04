using Microsoft.AspNetCore.Http;
using MongoDB.Driver.GridFS;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class PatchesService : MongoBaseService<Patch>
    {
        public PatchesService(MongoService mongo) : base(mongo, "Patches")
        {
        }

        public async Task<Patch> Save(string project, IFormFile file)
        {
            IGridFSBucket gridFS = new GridFSBucket(Collection.Database);
            var id = await gridFS.UploadFromStreamAsync(file.FileName, file.OpenReadStream());

            var patch = new Patch
            {
                Project = project,
                FileId = id.ToString()
            };

            await Insert(patch);
            return patch;
        }
    }
}

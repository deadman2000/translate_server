using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using System.IO;
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
                FileName = file.FileName,
                FileId = id.ToString()
            };

            await Insert(patch);
            return patch;
        }

        public async Task Download(string fileId, Stream destStream)
        {
            IGridFSBucket gridFS = new GridFSBucket(Collection.Database);
            await gridFS.DownloadToStreamAsync(new ObjectId(fileId), destStream);
        }
    }
}

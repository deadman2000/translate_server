using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using System;
using System.IO;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class PatchesService : MongoBaseService<Patch>
    {
        private readonly GridFSBucket gridFS;

        public PatchesService(MongoService mongo) : base(mongo, "Patches")
        {
            gridFS = new GridFSBucket(Collection.Database);
        }

        public async Task<Patch> Save(string project, IFormFile file, string user)
        {
            var id = await gridFS.UploadFromStreamAsync(file.FileName, file.OpenReadStream());

            var patch = new Patch
            {
                Project = project,
                FileName = file.FileName.ToLower(),
                FileId = id.ToString(),
                User = user,
                UploadDate = DateTime.UtcNow
            };

            await Insert(patch);
            return patch;
        }

        public async Task Update(Patch patch, IFormFile file)
        {
            await gridFS.DeleteAsync(new ObjectId(patch.FileId));
            var id = await gridFS.UploadFromStreamAsync(file.FileName, file.OpenReadStream());
            patch.FileId = id.ToString();
            await Update(p => p.Id == patch.Id).Set(p => p.FileId, patch.FileId).Execute();
        }

        public async Task Download(string fileId, Stream destStream)
        {
            await gridFS.DownloadToStreamAsync(new ObjectId(fileId), destStream);
        }

        public async Task Delete(string id)
        {
            var patch = await Get(p => p.Id == id);
            if (patch == null) return;

            await gridFS.DeleteAsync(new ObjectId(patch.FileId));
            await DeleteOne(p => p.Id == id);
        }
    }
}

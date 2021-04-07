using System.IO;
using System.IO.Compression;

namespace TranslateServer.Helpers
{
    public static class ZipHelpers
    {
        public static void ExtractSubDir(this ZipArchive archive, string targetDir, string archiveDir)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(archiveDir)) continue;
                var subName = entry.FullName[archiveDir.Length..];
                if (subName.Length == 0) continue;

                string fullPath = Path.Combine(targetDir, subName);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    if (!entry.Name.Equals("please dont extract me.txt"))
                    {
                        entry.ExtractToFile(fullPath);
                    }
                }
            }
        }
    }
}

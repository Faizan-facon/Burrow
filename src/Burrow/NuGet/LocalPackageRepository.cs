using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet
{
    public interface IPackageRepository
    {
        string Source { get; }
        IEnumerable<IPackage> FindPackagesById(string id);
    }

    public class LocalPackageRepository : IPackageRepository
    {
        private readonly string repositoryPath;

        public LocalPackageRepository(string path)
        {
            repositoryPath = path;
            Source = path;
        }

        public string Source { get; }

        public IEnumerable<IPackage> FindPackagesById(string id)
        {
            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
                return Enumerable.Empty<IPackage>();

            var results = new List<IPackage>();
            var files = Directory.GetFiles(repositoryPath, $"{id}.*.nupkg", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var package = new ZipPackage(file);
                    if (string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(package);
                    }
                }
                catch
                {
                    // Ignore corrupted or unrelated packages
                }
            }

            return results;
        }
    }
}

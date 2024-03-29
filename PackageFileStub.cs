using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace NuGetPackageMerge
{
    internal class PackageFileStub : IPackageFile
    {
        #region Implementation of IPackageFile

        public Stream GetStream()
        {
            return null;
        }

        public string Path { get; set; }
        public string EffectivePath { get; set; }
        public FrameworkName TargetFramework { get; set; }
        public NuGetFramework NuGetFramework { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }

        #endregion
    }
}

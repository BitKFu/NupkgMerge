// Copyright (c) 2015 Abel Cheng <abelcys@gmail.com>. Licensed under the MIT license.
// Repository: https://nupkgmerge.codeplex.com/

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Threading;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetPackageMerge
{
	class NupkgMerge
	{
		private readonly PackageBuilder _builder;
        private List<string> _folderToRemove = [];
        private List<string> _filesToRemove = [];
 
		public NupkgMerge(string primaryNupkg)
		{
			_builder = new PackageBuilder();
			Merge(primaryNupkg, true);
		}

		private static void ReplaceMetadata(PackageBuilder builder, NuspecReader primarySource)
		{
			if (!string.IsNullOrEmpty(primarySource.GetId()))
				builder.Id = primarySource.GetId();
			if (primarySource.GetVersion() != null)
				builder.Version = primarySource.GetVersion();
			if (!string.IsNullOrEmpty(primarySource.GetTitle()))
				builder.Title = primarySource.GetTitle();
			if (!string.IsNullOrEmpty(primarySource.GetIconUrl()))
				builder.IconUrl = new Uri(primarySource.GetIconUrl());
			if (!string.IsNullOrEmpty(primarySource.GetLicenseUrl()))
				builder.LicenseUrl = new Uri(primarySource.GetLicenseUrl());
			if (!string.IsNullOrEmpty(primarySource.GetProjectUrl()))
				builder.ProjectUrl = new Uri(primarySource.GetProjectUrl());
			if (primarySource.GetRequireLicenseAcceptance())
				builder.RequireLicenseAcceptance = primarySource.GetRequireLicenseAcceptance();
			if (primarySource.GetDevelopmentDependency())
				builder.DevelopmentDependency = primarySource.GetDevelopmentDependency();
			if (!string.IsNullOrEmpty(primarySource.GetDescription()))
				builder.Description = primarySource.GetDescription();
			if (!string.IsNullOrEmpty(primarySource.GetSummary()))
				builder.Summary = primarySource.GetSummary();
			if (!string.IsNullOrEmpty(primarySource.GetReleaseNotes()))
				builder.ReleaseNotes = primarySource.GetReleaseNotes();
			if (!string.IsNullOrEmpty(primarySource.GetCopyright()))
				builder.Copyright = primarySource.GetCopyright();
			if (!string.IsNullOrEmpty(primarySource.GetLanguage()))
				builder.Language = primarySource.GetLanguage();
			if (primarySource.GetMinClientVersion() != null)
				builder.MinClientVersion = primarySource.GetMinClientVersion().Version;
			if (!string.IsNullOrEmpty(primarySource.GetTags()))
				builder.Tags.AddRange(SplitTags(primarySource.GetTags()));
			builder.Authors.AddRange(primarySource.GetAuthors().Split(',').Except(builder.Authors));
			builder.Owners.AddRange(primarySource.GetOwners().Split(',').Except(builder.Owners));

			MergeDependencySets(builder.DependencyGroups, primarySource.GetDependencyGroups());
            MergeFrameworkReferences(builder.FrameworkReferenceGroups, primarySource.GetFrameworkRefGroups());
            MergePackageAssemblyReferences(builder.FrameworkReferences, primarySource.GetFrameworkAssemblyGroups());
			MergeContentFiles(builder.ContentFiles, primarySource.GetContentFiles());
        }

		private static void MergeMetadata(PackageBuilder builder, NuspecReader secondSource)
		{
			if (string.IsNullOrEmpty(builder.Id) && !string.IsNullOrEmpty(secondSource.GetId()))
				builder.Id = secondSource.GetId();
			if (builder.Version == null && secondSource.GetVersion() != null)
				builder.Version = secondSource.GetVersion();
			if (string.IsNullOrEmpty(builder.Title) && !string.IsNullOrEmpty(secondSource.GetTitle()))
				builder.Title = secondSource.GetTitle();
			if (builder.IconUrl == null && !string.IsNullOrEmpty(secondSource.GetIconUrl()))
				builder.IconUrl = new Uri(secondSource.GetIconUrl());
			if (builder.LicenseUrl == null && !string.IsNullOrEmpty(secondSource.GetLicenseUrl()))
				builder.LicenseUrl = new Uri(secondSource.GetLicenseUrl());
			if (builder.ProjectUrl == null && !string.IsNullOrEmpty(secondSource.GetProjectUrl()))
				builder.ProjectUrl = new Uri(secondSource.GetProjectUrl());
			if (!builder.RequireLicenseAcceptance && secondSource.GetRequireLicenseAcceptance())
				builder.RequireLicenseAcceptance = secondSource.GetRequireLicenseAcceptance();
			if (!builder.DevelopmentDependency && secondSource.GetDevelopmentDependency())
				builder.DevelopmentDependency = secondSource.GetDevelopmentDependency();
			if (string.IsNullOrEmpty(builder.Description) && !string.IsNullOrEmpty(secondSource.GetDescription()))
				builder.Description = secondSource.GetDescription();
			if (string.IsNullOrEmpty(builder.Summary) && !string.IsNullOrEmpty(secondSource.GetSummary()))
				builder.Summary = secondSource.GetSummary();
			if (string.IsNullOrEmpty(builder.ReleaseNotes) && !string.IsNullOrEmpty(secondSource.GetReleaseNotes()))
				builder.ReleaseNotes = secondSource.GetReleaseNotes();
			if (string.IsNullOrEmpty(builder.Copyright) && !string.IsNullOrEmpty(secondSource.GetCopyright()))
				builder.Copyright = secondSource.GetCopyright();
			if (string.IsNullOrEmpty(builder.Language) && !string.IsNullOrEmpty(secondSource.GetLanguage()))
				builder.Language = secondSource.GetLanguage();
			if (builder.MinClientVersion == null && secondSource.GetMinClientVersion() != null)
				builder.MinClientVersion = secondSource.GetMinClientVersion().Version;
			if (!string.IsNullOrEmpty(secondSource.GetTags()))
				builder.Tags.AddRange(SplitTags(secondSource.GetTags()).Except(builder.Tags));
			builder.Authors.AddRange(secondSource.GetAuthors().Split(',').Except(builder.Authors));
			builder.Owners.AddRange(secondSource.GetOwners().Split(',').Except(builder.Owners));

			MergeDependencySets(builder.DependencyGroups, secondSource.GetDependencyGroups());
			MergeFrameworkReferences(builder.FrameworkReferenceGroups, secondSource.GetFrameworkRefGroups());
			MergePackageAssemblyReferences(builder.FrameworkReferences, secondSource.GetFrameworkAssemblyGroups());
            MergeContentFiles(builder.ContentFiles, secondSource.GetContentFiles());
        }


        private static void MergeDependencySets(Collection<PackageDependencyGroup> dependencySets, IEnumerable<PackageDependencyGroup> secondDependencies)
		{
			if (dependencySets.Count == 0)
				dependencySets.AddRange(secondDependencies);
			else
			{
                foreach (var newDepSet in secondDependencies)
                {
                    var oldDepSet = dependencySets.FirstOrDefault(d => d.TargetFramework == newDepSet.TargetFramework);

                    if (oldDepSet == null)
						dependencySets.Add(newDepSet);
					else
                    {
                        var packages = oldDepSet.Packages.ToList();
						
                        foreach (var dep in newDepSet.Packages)
							if (!oldDepSet.Packages.Any(d => d.Id.Equals(dep.Id, StringComparison.OrdinalIgnoreCase)))
								packages.Add(dep);

                        dependencySets.Remove(oldDepSet);
						dependencySets.Add(new PackageDependencyGroup(oldDepSet.TargetFramework, packages));
                    }
                }
			}
		}

		private static void MergeFrameworkReferences(Collection<FrameworkReferenceGroup> frameworkReferences, IEnumerable<FrameworkReferenceGroup> secondFrameworkReferences)
		{
            foreach (var newRefSet in secondFrameworkReferences)
            {
                var existingFrameworkReference = frameworkReferences.FirstOrDefault(p => p.TargetFramework == newRefSet.TargetFramework)
                                                 ?? new FrameworkReferenceGroup(newRefSet.TargetFramework, newRefSet.FrameworkReferences);

                // Merge references
                var copiedReferences = existingFrameworkReference.FrameworkReferences.ToList();
                foreach (var r in newRefSet.FrameworkReferences)
					if (!copiedReferences.Contains(r))
						copiedReferences.Add(r);

                // Remove the old package reference and add the merged 
                frameworkReferences.Remove(existingFrameworkReference);
                frameworkReferences.Add(new FrameworkReferenceGroup(existingFrameworkReference.TargetFramework, copiedReferences));
            }
		}

		private static void MergePackageAssemblyReferences(Collection<FrameworkAssemblyReference> packageAssemblyReferences, IEnumerable<FrameworkSpecificGroup> secondPackageAssemblyReferences)
		{
            foreach (var newPrSet in secondPackageAssemblyReferences)
            {
                foreach (var assemblyFile in newPrSet.Items)
                {
                    FrameworkAssemblyReference newReference = null;

                    var assemblyReference = packageAssemblyReferences.FirstOrDefault(p => p.AssemblyName == assemblyFile);
                    if (assemblyReference != null && !assemblyReference.SupportedFrameworks.Contains(newPrSet.TargetFramework))
                        newReference = new FrameworkAssemblyReference(assemblyFile, assemblyReference.SupportedFrameworks.Concat([newPrSet.TargetFramework]));
                    else
                    {
                        if (assemblyReference == null)
                            newReference = new FrameworkAssemblyReference(assemblyFile, [newPrSet.TargetFramework]);
                    }

					// Remove and add again
					if (newReference == null)
						continue;

                    packageAssemblyReferences.Remove(packageAssemblyReferences.FirstOrDefault(p => p.AssemblyName == assemblyFile));
					packageAssemblyReferences.Add(newReference);
                }
            }
		}

        private static void MergeContentFiles(ICollection<ManifestContentFiles> builderContentFiles, IEnumerable<ContentFilesEntry> secondContentFiles)
        {
            foreach (var newContentFile in secondContentFiles)
            {
                var mfc = new ManifestContentFiles()
                {
					BuildAction = newContentFile.BuildAction,
					CopyToOutput = newContentFile.CopyToOutput?.ToString(),
					Exclude = newContentFile.Exclude,
					Flatten = newContentFile.Flatten?.ToString(),
					Include = newContentFile.Include
                };
                builderContentFiles.Add(mfc);
            }
        }


        public void Merge(string secondNupkg, bool replaceMetadata = false)
		{
			var secondPackage = new PackageArchiveReader(new ZipArchive(new FileStream(secondNupkg, FileMode.Open)));

			if (replaceMetadata)
				ReplaceMetadata(_builder, secondPackage.NuspecReader);
			else
				MergeMetadata(_builder, secondPackage.NuspecReader);

            var tempPath = Directory.GetCurrentDirectory();
            secondPackage.CopyFiles(tempPath, secondPackage.GetFiles(), CopyProgress, null, CancellationToken.None);
			foreach (var packageFile in secondPackage.GetFiles())
                if (!_builder.Files.Any(e => e.Path.Equals(packageFile, StringComparison.OrdinalIgnoreCase)))
                {
                    var nugetPackageFile = new PhysicalPackageFile() { TargetPath = packageFile, SourcePath = Path.Combine(tempPath, packageFile)};
                    _builder.Files.Add(nugetPackageFile);
                }
        }

        private string CopyProgress(string sourcefile, string targetFile, Stream filestream)
        {
            Console.WriteLine($"{sourcefile} -> {targetFile}");

            byte[] target = new byte[filestream.Length];
            filestream.Read(target, 0, (int)filestream.Length);

            var targetPath = Path.GetDirectoryName(targetFile);
            _folderToRemove.Add(targetPath);

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            File.WriteAllBytes(targetFile, target);
			_filesToRemove.Add(targetFile);
            return sourcefile;
        }

        public void CleanUpLocalFiles()
        {
            foreach (var fileName in _filesToRemove)
            {
                File.Delete(fileName);
            }

            foreach (var folderName in _folderToRemove)
            {
                try
                {
                    var innerFolder = folderName;
                    while (innerFolder != null && Directory.GetFiles(innerFolder).Length == 0)
                    {
                        try
                        {
                            Directory.Delete(innerFolder);
                            innerFolder = Directory.GetParent(innerFolder)?.FullName;
                        }
                        catch
                        {
                            // thrown if the folder is not empty
                            innerFolder = null;
                        }
                    }
                }
                catch
                {
					// Folder not found (because we deleted it earlier)
                }
            }

            _filesToRemove = [];
            _folderToRemove = [];
        }

        public void Save(string outputNupkg)
        {
            using Stream stream = File.Create(outputNupkg);
            _builder.Save(stream);
        }

		private static IEnumerable<string> SplitTags(string tags)
		{
			return tags?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>();
		}
	}
}

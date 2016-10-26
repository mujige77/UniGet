using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json.Linq;

namespace UniGet
{
    internal static class Extracter
    {
        public static void ExtractUnityPackage(string packageFile, string outputDir, string projectId, bool includeExtra,
            bool includeMerged)
        {
            var tempPath = CreateTemporaryDirectory();

            // unzip all files in package into temp dir

            using (var fileStream = new FileStream(packageFile, System.IO.FileMode.Open, FileAccess.Read))
            using (var gzipStream = new GZipInputStream(fileStream))
            {
                var tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
                tarArchive.ExtractContents(tempPath);
                tarArchive.Close();
            }

            // reconstruct directory structure

            var files = new Dictionary<string, string>();
            var folders = new Dictionary<string, string>();

            foreach (var guid in Directory.GetDirectories(tempPath))
            {
                var assetFile = Path.Combine(guid, "asset");
                var assetMetaFile = Path.Combine(guid, "asset.meta");
                var pathNameFile = Path.Combine(guid, "pathname");

                if (File.Exists(pathNameFile) == false)
                    continue;

                var path = File.ReadAllText(pathNameFile).Trim();
                var lines = File.ReadAllLines(pathNameFile);
                if (File.Exists(assetFile))
                    files.Add(lines[0], assetFile);
                else
                    folders.Add(lines[0], assetFile);
            }

            // read project in package and get file type information

            var fileMap = new Dictionary<string, Project.FileItem>();

            var projectFile = $"Assets/UnityPackages/{projectId}.unitypackage.json";
            var projectFilePath = "";
            if (files.TryGetValue(projectFile, out projectFilePath))
            {
                var p = Project.Load(projectFilePath);
                if (p.Files != null)
                {
                    foreach (var fileValue in p.Files)
                    {
                        if (fileValue is JObject)
                        {
                            var fileItem = fileValue.ToObject<Project.FileItem>();
                            fileMap.Add(fileItem.Target, fileItem);
                        }
                        else
                        {
                            fileMap.Add(fileValue.ToString(), new Project.FileItem { Target = fileValue.ToString() });
                        }
                    }
                }
            }

            var folderForAddedFile = new HashSet<string>();
            foreach (var file in files)
            {
                Project.FileItem fileItem;
                if (fileMap.TryGetValue(file.Key, out fileItem))
                {
                    if (includeExtra == false && fileItem.Extra)
                        continue;
                    if (includeMerged == false && fileItem.Merged)
                        continue;
                }

                // copy file

                var destPath = Path.Combine(outputDir, file.Key);
                if (destPath.Contains("/UnityPackages") == false)
                    destPath = destPath.Insert(6, "/UnityPackages");
                var destDirPath = Path.GetDirectoryName(destPath);

                if (Directory.Exists(destDirPath) == false)
                    Directory.CreateDirectory(destDirPath);

                while (true)
                {
                    try
                    {
                        FileCopyIfNewFile(file.Value, destPath);
                        if (File.Exists(file.Value + ".meta"))
                            FileCopyIfNewFile(file.Value + ".meta", destPath + ".meta");
                        break;
                    }
                    catch (System.UnauthorizedAccessException ex)
                    {
                        Console.WriteLine(ex.Message);
                        Thread.Sleep(10);
                        continue;
                    }
                }

                // mark directory for copying directory meta file later

                var dirPath = Path.GetDirectoryName(file.Key).Replace("\\", "/");
                while (string.IsNullOrEmpty(dirPath) == false && folderForAddedFile.Add(dirPath))
                {
                    dirPath = Path.GetDirectoryName(dirPath).Replace("\\", "/");
                }
            }

            foreach (var folder in folderForAddedFile)
            {
                string folderPath;
                if (folders.TryGetValue(folder, out folderPath))
                {
                    var destPath = Path.Combine(outputDir, folder);
                    while (true)
                    {
                        try
                        {
                            if (File.Exists(folderPath + ".meta"))
                                FileCopyIfNewFile(folderPath + ".meta", destPath + ".meta");
                            break;
                        }
                        catch (System.UnauthorizedAccessException ex)
                        {
                            Console.WriteLine(ex.Message);
                            Thread.Sleep(10);
                            continue;
                        }
                    }
                }
            }

            Directory.Delete(tempPath, true);
        }

        public static void FileCopyIfNewFile(string src, string dest)
        {
            if (IsNewFile(src, dest))
            {
                File.Copy(src, dest, true);
            }
        }

        public static bool IsNewFile(string src, string dest)
        {
            FileInfo destFile = new FileInfo(dest);
            if (destFile.Exists)
            {
                FileInfo file = new FileInfo(src);
                return file.LastWriteTime > destFile.LastWriteTime;
            }
            return true;
        }

        public static string CreateTemporaryDirectory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static Func<string, bool> MakeFilter(IEnumerable<Regex> filters)
        {
            return p => filters.Any(f => f.IsMatch(p));
        }

        public static Func<string, bool> MakeSampleFilter()
        {
            return p => Path.GetDirectoryName(p).ToLower().Contains("sample");
        }

        public static Func<string, bool> MakeFilter(List<string> includes, List<string> excludes)
        {
            var excludeSample = false;
            var idx = excludes.FindIndex(f => f.ToLower() == "$sample$");
            if (idx != -1)
            {
                excludeSample = true;
                excludes = new List<string>(excludes);
                excludes.RemoveAt(idx);
            }

            var excludeSampleFilter = excludeSample ? MakeSampleFilter() : null;
            var excludeFilter = excludes.Any() ? MakeFilter(excludes.Select(f => new Regex(f)).ToList()) : null;
            var includeFilter = includes.Any() ? MakeFilter(includes.Select(f => new Regex(f)).ToList()) : null;

            return s =>
            {
                if (excludeSampleFilter != null && excludeSampleFilter(s))
                    return false;
                if (excludeFilter != null && excludeFilter(s))
                    return false;
                return (includeFilter != null) ? includeFilter(s) : true;
            };
        }
    }
}
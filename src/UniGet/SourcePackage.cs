using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;

namespace UniGet
{
    public class SourcePackage
    {
        public static bool ExtractUnityPackage(string packageFile, string outputDir, string projectId, bool includeExtra, bool includeMerged)
        {
            var createProjectFile = false;
            var tempPath = CreateTemporaryDirectory();

            // unzip all files in package into temp dir
            var fastZip = new FastZip();
            fastZip.ExtractZip(packageFile, tempPath, FastZip.Overwrite.Always, overlite => true, null, null, true);

            var target = Path.Combine("Assets/UnityPackages", projectId);
            var destPath = Path.Combine(outputDir, target);

            if (Directory.Exists(destPath) == false)
            {
                Directory.CreateDirectory(destPath);
                File.WriteAllBytes(destPath + ".meta", Packer.GenerateMeta(destPath, destPath).Item2);
            }

             var projectFile = Path.Combine(tempPath, $"{projectId}.unitypackage.json");
            if (File.Exists(projectFile) == false)
                createProjectFile = true;

            CopyFilesRecursively(new DirectoryInfo(tempPath), new DirectoryInfo(destPath));

            Directory.Delete(tempPath, true);
            return createProjectFile;
        }

        public static string CreateTemporaryDirectory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                DirectoryInfo subDirectory;
                var subDirectoryPath = Path.Combine(target.FullName, dir.Name);
                if (Directory.Exists(subDirectoryPath))
                {
                    subDirectory = new DirectoryInfo(subDirectoryPath);
                }
                else
                {
                     subDirectory = target.CreateSubdirectory(dir.Name);
                     File.WriteAllBytes(subDirectoryPath + ".meta", Packer.GenerateMeta(target.FullName, target.FullName).Item2);
                }
                CopyFilesRecursively(dir, subDirectory);
            }
            foreach (FileInfo file in source.GetFiles())
            {
                if (IsIgnoreFile(file.FullName) == false)
                {
                    var fullpath = Path.Combine(target.FullName, file.Name);
                    if (File.Exists(fullpath) == false)
                        File.WriteAllBytes(fullpath + ".meta", Packer.GenerateMeta(fullpath, fullpath).Item2);
                    file.CopyTo(fullpath, true);
                }
            }
        }

        public static bool IsIgnoreFile(string path)
        {
            var lowerStr = path.ToLower();
            if (lowerStr.ToLower().Contains(".ds_store"))
                return true;

            return false;
        }
    }
}
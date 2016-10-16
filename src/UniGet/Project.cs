using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UniGet
{
    internal class Project
    {
#pragma warning disable 0649

        public string Id;
        public string Version;

        public string Title;                // Human readable name
        public List<string> Authors;        // List of authors
        public List<string> Owners;         // List of owners
        public string Description;          // Description

        public class Dependency
        {
            public string Version;          // SemVer version (e.g. 1.5.20-beta)
            public string Source;           // Download source (local, github:author/asset, nuget:tfm)
            public bool IncludeExtra;       // Inlcude extra contents
            public bool IncludeMerged;      // Include merged dependencies%
            public string SourceType;       // Download Source Type
        }

        public Dictionary<string, Dependency> Dependencies;
        public Dictionary<string, Dependency> MergedDependencies;

        public List<JToken> Files;

        internal class FileItem
        {
            public string Source;
            public string Target;
            public bool Extra;
            public bool Merged;
        }

#pragma warning restore 0649

        // Loader

        public static Project Load(string filePath)
        {
            var json = LoadJson(filePath, new HashSet<string>());
            return json.ToObject<Project>();
        }

        private static JObject LoadJson(string filePath, HashSet<string> openSet)
        {
            if (openSet.Add(Path.GetFullPath(filePath)) == false)
                throw new InvalidDataException("#base repeats!");

            var json = JObject.Parse(File.ReadAllText(filePath));

            var baseFile = json.GetValue("#base");
            if (baseFile == null)
                return json;

            json.Remove("#base");

            var baseJson = LoadJson(Path.Combine(Path.GetDirectoryName(filePath), baseFile.ToString()), openSet);
            baseJson.Merge(json);
            return baseJson;
        }
    }
}

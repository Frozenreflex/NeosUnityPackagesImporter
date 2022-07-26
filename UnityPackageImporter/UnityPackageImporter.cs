using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityPackageImporter.Extractor;

namespace UnityPackageImporter
{
    public class UnityPackageImporter : NeosMod
    {
        public override string Name => "UnityPackageImporter";
        public override string Author => "dfgHiatus, eia485, delta";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/NeosUnityPackagesImporter";
        public static ModConfiguration config;
        private static UnityPackageExtractor extractor = new UnityPackageExtractor();
        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.UnityPackageImporter").PatchAll();
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => AssetPatch());
        }
        private static void AssetPatch()
        {
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Special].Add("unitypackage");
        }

        [HarmonyPatch(typeof(UniversalImporter), "Import")]
        public class UniversalImporterPatch
        {
            private static bool Prefix(ref IEnumerable<string> files)
            {
                var unitypackages = files.Where(f => Path.GetExtension(f).ToLower() == ".unitypackage");
                foreach (var unitypackage in unitypackages) // Parallel.For here?
                {
                    // Should add decomposed files and remove duplicates??
                    files.Union(DecomposeUnityPackage(unitypackage));
                }
                
                return true;
            }
        }

        public static IEnumerable<string> DecomposeUnityPackage(string unitypackage)
        {
            var modelName = Path.GetFileNameWithoutExtension(unitypackage);
            if (ContainsUnicodeCharacter(modelName))
            {
                throw new ArgumentException("Imported unity package cannot have unicode characters in its file name.");
            }

            var trueCachePath = Path.Combine(Engine.Current.CachePath, "Cache");
            var time = DateTime.Now.Ticks.ToString();

            var extractedPath = Path.Combine(trueCachePath, modelName + "_" + time);
            extractor.Unpack(unitypackage, extractedPath); // Blocks until extracted

            List<string> imports = new List<string>();
            foreach (var file in new DirectoryInfo(extractedPath).EnumerateFiles())
            {
                if (AssetHelper.ClassifyExtension(file.Extension) == AssetClass.Model ||
                    AssetHelper.ClassifyExtension(file.Extension) == AssetClass.Texture ||
                    AssetHelper.ClassifyExtension(file.Extension) == AssetClass.Audio ||
                    AssetHelper.ClassifyExtension(file.Extension) == AssetClass.Shader)
                {
                    imports.Add(file.FullName);
                }
            }
            return imports;
        }

        private static bool ContainsUnicodeCharacter(string input)
        {
            const int MaxAnsiCode = 255;
            return input.Any(c => c > MaxAnsiCode);
        }
    }
}
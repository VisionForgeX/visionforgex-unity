using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VfxAds.EditorTools
{
    /// <summary>
    /// Where the plugin keeps the game's settings (Assets/VisionForgeX/Resources/VisionForgeXSettings.asset) and the
    /// External Dependency Manager's file it writes from them (Assets/VisionForgeX/Editor/VisionForgeXDependencies.xml).
    /// Both are made the first time the project opens with the plugin, so a build is never missing them.
    /// </summary>
    [InitializeOnLoad]
    internal static class VfxSettingsStore
    {
        public const string Folder = "Assets/VisionForgeX";
        public const string AssetPath = Folder + "/Resources/" + VfxSettings.ResourceName + ".asset";
        public const string DependenciesPath = Folder + "/Editor/VisionForgeXDependencies.xml";

        static VfxSettingsStore()
        {
            // after the import that loaded this code, never during it
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var s = Load();
                if (s != null) WriteDependencies(s);
            };
        }

        /// <summary>The settings, or null when none were made yet.</summary>
        public static VfxSettings Load() => AssetDatabase.LoadAssetAtPath<VfxSettings>(AssetPath);

        /// <summary>The settings, made (with their defaults) if there are none yet.</summary>
        public static VfxSettings LoadOrCreate()
        {
            var s = Load();
            if (s != null) return s;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            s = ScriptableObject.CreateInstance<VfxSettings>();
            AssetDatabase.CreateAsset(s, AssetPath);
            AssetDatabase.SaveAssets();
            WriteDependencies(s);
            Debug.Log("[VisionForgeX] Settings made at " + AssetPath + ". Fill them in: Assets → VisionForgeX → Settings.");
            return s;
        }

        /// <summary>Writes the dependencies file when its text would change (so the resolver runs only when it must).</summary>
        public static bool WriteDependencies(VfxSettings s)
        {
            var xml = VfxNativeSetup.DependenciesXml(s.androidEngine, VisionForgeX.AndroidSdkVersion, VisionForgeX.IosSdkVersion);
            try
            {
                if (File.Exists(DependenciesPath) && File.ReadAllText(DependenciesPath) == xml) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(DependenciesPath));
                File.WriteAllText(DependenciesPath, xml);
                AssetDatabase.ImportAsset(DependenciesPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[VisionForgeX] Could not write " + DependenciesPath + ": " + e.Message);
                return false;
            }
        }

        public static void Save(VfxSettings s)
        {
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssetIfDirty(s);
            WriteDependencies(s);
        }
    }
}

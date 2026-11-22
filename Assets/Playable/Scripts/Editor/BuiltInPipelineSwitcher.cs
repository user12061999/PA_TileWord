using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace PA.TileWord.EditorTools
{
    /// <summary>
    /// Luna Playworks không serialize được URP (Renderer2DData.copyDepthMode trên Unity 6),
    /// playable chỉ dùng uGUI nên chuyển về Built-in Render Pipeline và gỡ package URP.
    /// Tự chạy khi phát hiện project còn dùng URP; chạy lại bằng menu Playable/Switch To Built-in RP.
    /// </summary>
    [InitializeOnLoad]
    public static class BuiltInPipelineSwitcher
    {
        private const string UrpPackage = "com.unity.render-pipelines.universal";

        private static readonly string[] UrpAssets =
        {
            "Assets/Settings",
            "Assets/DefaultVolumeProfile.asset",
            "Assets/UniversalRenderPipelineGlobalSettings.asset",
        };

        static BuiltInPipelineSwitcher()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UsesSrp() || UrpInManifest()) Switch();
        }

        [MenuItem("Playable/Switch To Built-in RP")]
        public static void Switch()
        {
            GraphicsSettings.defaultRenderPipeline = null;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();

            foreach (string path in UrpAssets)
            {
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }

            if (UrpInManifest())
            {
                Debug.Log("[Playable] Đã chuyển sang Built-in RP, đang gỡ package " + UrpPackage);
                Client.Remove(UrpPackage);
            }
        }

        private static bool UsesSrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return true;
            int current = QualitySettings.GetQualityLevel();
            bool any = false;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) != null) any = true;
            }
            QualitySettings.SetQualityLevel(current, false);
            return any;
        }

        private static bool UrpInManifest()
        {
            string manifest = "Packages/manifest.json";
            return File.Exists(manifest) && File.ReadAllText(manifest).Contains("\"" + UrpPackage + "\"");
        }
    }
}

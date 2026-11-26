using System;
using System.Collections.Generic;
using PA.TileWord;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace PA.TileWord.EditorTools
{
    /// <summary>
    /// Dựng scene playable, sinh font TMP static (Luna không hỗ trợ dynamic atlas), tối ưu texture,
    /// dọn tài nguyên TMP mặc định.
    /// Tự chạy 1 lần khi scene chưa tồn tại; chạy lại bằng menu Playable/Setup Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayableSetup
    {
        private const string Root = "Assets/Playable";
        private const string ScenePath = Root + "/Scenes/Playable.unity";
        private const string FontTtfPath = Root + "/Fonts/Nunito-Black 1.ttf";
        private const string FontAssetPath = Root + "/Fonts/Nunito-Black SDF Static.asset";

        static PlayableSetup()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Setup(false);
            }
        }

        [MenuItem("Playable/Setup Scene")]
        public static void SetupFromMenu()
        {
            Setup(true);
        }

        private static void Setup(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool dirty = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty) dirty = true;
            }
            if (dirty)
            {
                if (!interactive)
                {
                    Debug.LogWarning("[Playable] Scene đang mở có thay đổi chưa lưu, bỏ qua auto-setup. Chạy menu Playable/Setup Scene.");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            ConfigureTextures();
            TMP_FontAsset font = BuildStaticFont();
            if (font == null) return;
            CleanupTmpDefaults(font);
            BuildScene(font);
            Debug.Log("[Playable] Setup xong: " + ScenePath);
        }

        // ------------------------------------------------------------ textures
        private static readonly string[] BigTextures =
        {
            "InGameBg.png", "TitleLogo.png", "StageInfoPanelTop.png", "StageInfoPanelMid.png", "StageInfoPanelBottom.png", "PlayButton.png", "PlayButtonHard.png",
            "ResultNicePopupPanel.png", "ResultLosePopupPanel.png",
        };

        private static readonly string[] MediumTextures = { "lose-icon-only.png" };


        private static void ConfigureTextures()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                string file = System.IO.Path.GetFileName(path);
                int maxSize = 256;
                if (Array.IndexOf(BigTextures, file) >= 0) maxSize = 1024;
                else if (Array.IndexOf(MediumTextures, file) >= 0) maxSize = 512;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------ font
        private static TMP_FontAsset BuildStaticFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            Font ttf = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (ttf == null)
            {
                Debug.LogError("[Playable] Không tìm thấy font " + FontTtfPath);
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            if (font == null)
            {
                Debug.LogError("[Playable] Tạo TMP font thất bại (kiểm tra TMP shaders trong Assets/TextMesh Pro/Shaders).");
                return null;
            }

            string chars = "";
            for (int c = 32; c < 127; c++) chars += (char)c;
            string missing;
            font.TryAddCharacters(chars, out missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[Playable] Font thiếu ký tự: " + missing);

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.name = "Nunito-Black SDF Static";

            AssetDatabase.CreateAsset(font, FontAssetPath);
            Texture2D atlas = font.atlasTexture;
            atlas.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
            Material material = font.material;
            material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        /// <summary>Đặt font mặc định TMP = font playable và xoá LiberationSans/EmojiOne khỏi Resources để nhẹ bundle.</summary>
        private static void CleanupTmpDefaults(TMP_FontAsset font)
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null) return;

            SerializedObject so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = null;
            SerializedProperty fallbacks = so.FindProperty("m_fallbackFontAssets");
            if (fallbacks != null) fallbacks.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            string[] removable =
            {
                "Assets/TextMesh Pro/Resources/Fonts & Materials",
                "Assets/TextMesh Pro/Resources/Sprite Assets",
                "Assets/TextMesh Pro/Fonts",
                "Assets/TextMesh Pro/Sprites",
            };
            foreach (string path in removable)
            {
                if (AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        // ------------------------------------------------------------ scene
        private static void BuildScene(TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(120, 170, 90, 255);
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            GameObject gameGo = new GameObject("PlayableGame");
            PlayableGame game = gameGo.AddComponent<PlayableGame>();
            game.backgroundSprite = LoadSprite("InGameBg");
            game.tileSprite = LoadSprite("LetterTile");
            game.slotSprite = LoadSprite("LetterTray");
            game.headerTopSprite = LoadSprite("StageInfoPanelTop");
            game.headerMidSprite = LoadSprite("StageInfoPanelMid");
            game.headerBottomSprite = LoadSprite("StageInfoPanelBottom");
            game.logoSprite = LoadSprite("TitleLogo");
            game.buttonSprite = LoadSprite("PlayButton");
            game.hardButtonSprite = LoadSprite("PlayButtonHard");
            game.handSprite = LoadSprite("Tutorial_Focus_Icon_Hand");
            game.sparkleSprite = LoadSprite("UnlockSparkle");
            game.starOnSprite = LoadSprite("symc_star");
            game.starOffSprite = LoadSprite("sym_star");
            game.crossSprite = LoadSprite("cross");
            game.pillCapSprite = LoadSprite("TrayWarningBarCap");
            game.flashEdgesSprite = LoadSprite("TrayWarningEdges");
            game.trayWarningSprite = LoadSprite("LetterTrayWarning");
            game.winPanelSprite = LoadSprite("ResultNicePopupPanel");
            game.losePanelSprite = LoadSprite("ResultLosePopupPanel");
            game.loseIconSprite = LoadSprite("lose-icon-only");
            game.font = font;
            game.music = LoadClip("music_main");
            game.sfxTap = LoadClip("tile click");
            game.sfxWrong = LoadClip("tile click blocked");
            game.sfxWoodLand = LoadClip("crate crack");
            game.sfxHeaderDrop = LoadClip("rubber quick");
            game.sfxStreak = LoadClip("power up");
            game.sfxSlotFill = LoadClip("coin pickup");
            game.sfxWord = LoadClip("coins reward");
            game.sfxWin = LoadClip("level complete");
            game.sfxButton = LoadClip("ui_button_1");

            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + name + ".png");
            if (sprite == null) Debug.LogError("[Playable] Thiếu sprite " + name);
            return sprite;
        }

        private static AudioClip LoadClip(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".wav");
            if (clip == null) Debug.LogError("[Playable] Thiếu audio " + name);
            return clip;
        }
    }
}

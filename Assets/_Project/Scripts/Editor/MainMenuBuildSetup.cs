using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Açılış ekranı (MainMenu) Build listesinde her zaman ilk sırada olsun: build'de oyun buradan açılır.
    /// Editör açılınca / script'ler derlenince kontrol edilir.
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuBuildSetup
    {
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";

        static MainMenuBuildSetup()
        {
            EditorApplication.delayCall += EnsureMainMenuFirst;
        }

        public static void EnsureMainMenuFirst()
        {
            if (!File.Exists(MainMenuPath)) return;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Count > 0 && scenes[0].path == MainMenuPath && scenes[0].enabled) return;

            scenes.RemoveAll(s => s == null || s.path == MainMenuPath);
            scenes.Insert(0, new EditorBuildSettingsScene(MainMenuPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log("[SoulsRPG] MainMenu Build listesinin başına eklendi.");
        }
    }
}

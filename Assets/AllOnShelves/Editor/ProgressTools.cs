using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Сохранения вернули (03.10.2026, GameApp.FreshStart = false). Начать игру с нуля — эта кнопка: стирает сейв
    /// редактора (Assets/PluginYourGames/Editor/SavesEditorYG2.json) и PlayerPrefs. Если игра запущена — сбрасывает
    /// прогресс сразу и перезапускает игру с заставки.
    /// </summary>
    public static class ProgressTools
    {
        const string EditorSave = "Assets/PluginYourGames/Editor/SavesEditorYG2.json";

        [MenuItem("Всё по полкам/Обнулить прогресс (начать игру заново)", priority = 4)]
        static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("Обнулить прогресс?",
                    "Сохранение игры в редакторе будет стёрто: уровни, звёзды, монеты, ремонт, альбом, Торговый дом, " +
                    "покупки и обучение — всё начнётся с нуля. Вернуть нельзя.",
                    "Обнулить", "Отмена"))
                return;
            ResetSilent();
            EditorUtility.DisplayDialog("Готово", Application.isPlaying
                ? "Прогресс обнулён, игра началась заново."
                : "Прогресс обнулён. Нажми Play — игра начнётся с первого уровня и обучения.", "Хорошо");
        }

        /// <summary>Сброс без вопросов (для скриптов и проверок).</summary>
        public static void ResetSilent()
        {
            if (Application.isPlaying && GameApp.I != null)
            {
                GameApp.I.ResetAllProgress();
                SceneManager.LoadScene(GameApp.SceneBoot);
                return;
            }
            if (File.Exists(EditorSave)) AssetDatabase.DeleteAsset(EditorSave);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[AllOnShelves] Прогресс обнулён (сейв редактора и PlayerPrefs стёрты)");
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Пауза: продолжить, заново, на карту, звук, музыка, как играть.</summary>
    public class PausePopup : Popup
    {
        public Button resumeButton;
        public Button restartButton;
        public Button mapButton;
        public Button soundButton;
        public Button musicButton;
        public Image soundIcon;
        public Image musicIcon;
        public Button helpButton;

        public void Setup(Action onResume, Action onRestart, Action onMap, Action onHelp)
        {
            Bind(resumeButton, onResume);
            Bind(restartButton, onRestart);
            Bind(mapButton, onMap);
            Bind(helpButton, onHelp);
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(() =>
            {
                var s = GameApp.I.Save;
                s.sfx = s.sfx > 0 ? 0 : 1;
                GameApp.I.MarkDirty();
                RefreshIcons();
            });
            musicButton.onClick.RemoveAllListeners();
            musicButton.onClick.AddListener(() =>
            {
                var s = GameApp.I.Save;
                s.music = s.music > 0 ? 0 : 0.7f;
                GameApp.I.MarkDirty();
                AudioService.ApplyVolumes();
                RefreshIcons();
            });
            RefreshIcons();
            Show();
        }

        void RefreshIcons()
        {
            var s = GameApp.I.Save;
            soundIcon.sprite = ArtLibrary.S(s.sfx > 0 ? "icon_sound_on" : "icon_sound_off");
            musicIcon.sprite = ArtLibrary.S(s.music > 0 ? "icon_music_on" : "icon_music_off");
        }

        static void Bind(Button b, Action a)
        {
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => a?.Invoke());
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Главное меню. Логотип, витрина и енот нарисованы на фоне (bg_hub_start),
    /// поверх — кнопка «Уровень N» и четыре плитки меню в правом нижнем углу.
    /// </summary>
    public class StartScreen : HubScreen
    {
        public Button playButton;
        public Button settingsButton;

        void Awake()
        {
            playButton.onClick.AddListener(OnPlay);
            settingsButton.onClick.AddListener(() => hub.settingsPopup.Open());
        }

        public override void Refresh()
        {
            Tween.Run(playButton.transform, Tween.ChScale, 1000f, Ease.Linear, k =>
            {
                if (playButton != null) playButton.transform.localScale = Vector3.one * (1f + Mathf.Sin(k * 1000f * 3f) * 0.04f);
            });
        }

        public override void Close()
        {
            Tween.Kill(playButton.transform);
            base.Close();
        }

        /// <summary>«Играть» открывает карту — она и есть главный экран игры.</summary>
        void OnPlay()
        {
            if (GameApp.I.BlockedByRenovation) { hub.Show(hub.renovationScreen); return; }
            hub.Show(hub.mapScreen);
        }
    }
}

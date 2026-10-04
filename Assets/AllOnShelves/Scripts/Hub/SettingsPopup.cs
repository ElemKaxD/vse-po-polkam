using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Настройки: громкость звука и музыки ползунками, значок — вкл/выкл. Покупки на Яндексе восстанавливаются сами при входе.</summary>
    public class SettingsPopup : Popup
    {
        public Button soundButton;
        public Button musicButton;
        public Image soundIcon;
        public Image musicIcon;
        public Slider soundSlider;
        public Slider musicSlider;
        public Button closeButton;
        public TextMeshProUGUI versionText;

        void Awake()
        {
            soundButton.onClick.AddListener(() =>
            {
                var s = GameApp.I.Save; s.sfx = s.sfx > 0 ? 0 : 1; GameApp.I.MarkDirty(); RefreshIcons();
            });
            musicButton.onClick.AddListener(() =>
            {
                var s = GameApp.I.Save; s.music = s.music > 0 ? 0 : 0.7f; GameApp.I.MarkDirty(); AudioService.ApplyVolumes(); RefreshIcons();
            });
            soundSlider.onValueChanged.AddListener(v =>
            {
                var s = GameApp.I.Save; s.sfx = v; GameApp.I.MarkDirty(); RefreshIcons();
            });
            musicSlider.onValueChanged.AddListener(v =>
            {
                var s = GameApp.I.Save; s.music = v; GameApp.I.MarkDirty(); AudioService.ApplyVolumes(); RefreshIcons();
            });
            closeButton.onClick.AddListener(() => Hide());
        }

        public void Open()
        {
            // пак звуков требует указать автора (лицензия «Cute & Cozy UI Audio»)
            versionText.text = "Версия " + Application.version + " · звуки: Case Portman Audio";
            RefreshIcons();
            Show();
        }

        void RefreshIcons()
        {
            var s = GameApp.I.Save;
            // значок нарисован на самой кнопке, меняем её картинку целиком
            soundIcon.sprite = ArtLibrary.S(s.sfx > 0 ? "btn_sound_on" : "btn_sound_off");
            musicIcon.sprite = ArtLibrary.S(s.music > 0 ? "btn_music_on" : "btn_music_off");
            soundSlider.SetValueWithoutNotify(s.sfx);
            musicSlider.SetValueWithoutNotify(s.music);
        }
    }
}

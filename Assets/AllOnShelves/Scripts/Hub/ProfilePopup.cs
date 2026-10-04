using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Профиль игрока: аватар со скином и ореолом, свой ник, счётчики на плитках и «Гардероб».
    /// Звук, музыка и «Звёздный путь» отсюда убраны (просьба 26.09.2026): звук живёт в настройках,
    /// награды за звёзды — на карте, их игрок открывает сам.
    /// </summary>
    public class ProfilePopup : Popup
    {
        public Image avatar;
        public Image halo;
        public TMP_InputField nameInput;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI[] statValues = new TextMeshProUGUI[4];
        public Button closeButton;
        public Button wardrobeButton;
        public TextMeshProUGUI versionText;

        HubController _hub;

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
            wardrobeButton.onClick.AddListener(() => { Hide(); _hub?.wardrobePopup.Open(); });
            nameInput.onEndEdit.AddListener(SaveName);
        }

        public void Open(HubController hub)
        {
            _hub = hub;
            Show();
            Refresh();
        }

        /// <summary>Ник задаёт игрок; пусто — берём имя из Яндекса, а его нет — «Хозяин магазина».</summary>
        public static string DisplayName()
        {
            var s = GameApp.I.Save;
            if (!string.IsNullOrWhiteSpace(s.playerName)) return s.playerName;
            return string.IsNullOrEmpty(Platform.PlayerName) ? "Хозяин магазина" : Platform.PlayerName;
        }

        void SaveName(string value)
        {
            value = (value ?? "").Trim();
            GameApp.I.Save.playerName = value;
            GameApp.I.MarkDirty();
            nameInput.SetTextWithoutNotify(DisplayName());
            AudioService.Play("sfx_click");
        }

        void Refresh()
        {
            var app = GameApp.I;
            var s = app.Save;
            avatar.sprite = app.FaceSprite(raccoonOnly: false);
            avatar.preserveAspect = true;
            var h = app.WornSprite("halo");
            halo.gameObject.SetActive(h != null);
            if (h != null) { halo.sprite = h; halo.preserveAspect = true; }

            nameInput.SetTextWithoutNotify(DisplayName());
            levelText.text = "Уровень " + app.CurrentLevel;

            if (statValues.Length > 0 && statValues[0] != null) statValues[0].text = s.maxReached + "/" + app.LevelCount;
            if (statValues.Length > 1 && statValues[1] != null) statValues[1].text = s.StarsTotal.ToString();
            if (statValues.Length > 2 && statValues[2] != null) statValues[2].text = s.stickers.Count.ToString();
            if (statValues.Length > 3 && statValues[3] != null) statValues[3].text = app.Coins.ToString();

            wardrobeButton.gameObject.SetActive(true);   // вещи продаются за монеты — гардероб нужен всегда
            versionText.text = "Версия " + Application.version;
        }
    }
}

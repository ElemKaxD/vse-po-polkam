using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Выбор режима сложности (v4, 03.10.2026): три карточки ui_diff_card — Лёгкий, Средний, Сложный (перчики icon_diff_*).
    /// На карточке: сколько пройдено, звёзды режима, множитель монет; закрытая — серая с замком и подсказкой,
    /// что открыть сначала. Тот же экран — окно «Новая сложность открыта!» (баннер diff_unlock_banner сверху).
    /// Логика — GameApp.Mode / ModeUnlocked / SetMode.
    /// </summary>
    public class DiffPopup : Popup
    {
        public Button[] cards = new Button[3];
        public Image[] cardImages = new Image[3];
        public Image[] icons = new Image[3];
        public TextMeshProUGUI[] names = new TextMeshProUGUI[3];
        public TextMeshProUGUI[] infos = new TextMeshProUGUI[3];
        public TextMeshProUGUI[] ribbons = new TextMeshProUGUI[3];
        public GameObject[] locks = new GameObject[3];
        public GameObject[] checks = new GameObject[3];
        public GameObject banner;
        public TextMeshProUGUI bannerText;
        public TextMeshProUGUI title;
        public Button closeButton;

        HubController _hub;

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
            for (int i = 0; i < cards.Length; i++) { int m = i; cards[i].onClick.AddListener(() => Pick(m)); }
        }

        /// <summary>announce — режим, который только что открылся (0 — просто выбор).</summary>
        public void Open(HubController hub, int announce = 0)
        {
            _hub = hub;
            banner.SetActive(announce > 0);
            if (announce > 0)
            {
                bannerText.text = $"Открыт режим «{GameApp.ModeNames[announce]}»!";
                GameApp.I.MarkModeAnnounced(announce);
                AudioService.Play("sfx_unlock");
                Vfx.At("vfx_difficulty_unlock", banner.transform, 760f, delay: 0.25f);
            }
            title.text = "Сложность";
            Refresh();
            Show();
            if (announce > 0) Tween.Punch(cards[announce].transform, 0.12f, 0.45f);
        }

        void Refresh()
        {
            var app = GameApp.I;
            int total = app.LevelCount;
            for (int m = 0; m < cards.Length; m++)
            {
                bool open = app.ModeUnlocked(m);
                bool on = app.Mode == m;
                int done = Mathf.Min(total, app.Save.Reached(m));
                icons[m].sprite = ArtLibrary.S(GameApp.ModeIcons[m]);
                icons[m].preserveAspect = true;
                names[m].text = GameApp.ModeNames[m];
                string coins = m == 0 ? "Монеты ×1" : $"Монеты ×{GameApp.ModeCoinMult[m]:0.##}";
                infos[m].text = open
                    ? $"Уровни {done}/{total}\nЗвёзды {app.Save.StarsIn(m)}/{total * 3}\n{coins}"
                    : $"Пройди все {total} уровней\nв режиме «{GameApp.ModeNames[m - 1]}»\n{coins}";
                ribbons[m].text = !open ? "Закрыто" : on ? "Выбран" : "Играть";
                locks[m].SetActive(!open);
                checks[m].SetActive(on);
                // закрытая карточка — серая (своей картинки «закрыто» нет: пришла другой формы)
                cardImages[m].color = open ? Color.white : new Color(0.62f, 0.60f, 0.58f, 1f);
                icons[m].color = open ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.85f);
            }
        }

        void Pick(int m)
        {
            var app = GameApp.I;
            if (!app.ModeUnlocked(m))
            {
                AudioService.Play("sfx_nope");
                Vfx.At("vfx_locked_shake", cards[m].transform, 220f);
                Toast.Show($"Сначала пройди все уровни в режиме «{GameApp.ModeNames[Mathf.Max(0, m - 1)]}»", GameApp.ModeIcons[m]);
                return;
            }
            AudioService.Play("sfx_button");
            if (app.Mode != m)
            {
                app.SetMode(m);
                app.Flush();
                Toast.Show($"Режим «{GameApp.ModeNames[m]}»: уровень {app.CurrentLevel}", GameApp.ModeIcons[m]);
            }
            Hide();
            if (_hub != null) { _hub.RefreshTop(); if (_hub.mapScreen != null) _hub.mapScreen.Refresh(); }
        }
    }
}

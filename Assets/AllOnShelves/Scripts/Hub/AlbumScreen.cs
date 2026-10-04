using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Альбом наклеек (кадр 11, ГДД 8.3): 10 отделов по 9 + золотая.</summary>
    public class AlbumScreen : HubScreen
    {
        public Button[] tabs = new Button[10];
        public Image[] tabIcons = new Image[10];
        public StickerCell[] cells = new StickerCell[9];
        public StickerCell goldCell;
        public TextMeshProUGUI deptTitle;
        public TextMeshProUGUI progressText;
        public Image progressFill;
        public GameObject deptRewardDone;
        [OptionalRef] public Image deptRewardIcon;   // награда отдела — его рамка аватарки (v4)
        [OptionalRef] public TMPro.TextMeshProUGUI bonusText;   // бонус коллекции (v4)
        public Button openPackButton;
        public TextMeshProUGUI openPackText;
        public GameObject openPackVideo;

        int _dept;
        float[] _tabX;

        /// <summary>Цвет ленточки-закладки каждого отдела (лента белая, тонируется).</summary>
        static readonly Color[] TabColors =
        {
            new Color(0.94f, 0.38f, 0.34f), new Color(0.48f, 0.74f, 0.36f), new Color(0.47f, 0.69f, 0.93f),
            new Color(0.89f, 0.64f, 0.36f), new Color(0.98f, 0.80f, 0.31f), new Color(0.32f, 0.74f, 0.76f),
            new Color(0.96f, 0.57f, 0.76f), new Color(0.64f, 0.52f, 0.89f), new Color(0.64f, 0.86f, 0.96f),
            new Color(0.72f, 0.86f, 0.36f),
        };

        /// <summary>Невыбранная закладка задвинута под страницы на столько.</summary>
        const float TabTucked = 26f;

        void Awake()
        {
            _tabX = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                _tabX[i] = ((RectTransform)tabs[i].transform).anchoredPosition.x;
                int d = i;
                tabs[i].onClick.AddListener(() => { _dept = d; Refresh(); });
            }
            openPackButton.onClick.AddListener(OnOpenPack);
        }

        public override void Open()
        {
            base.Open();
            // енот показывает, где открыть пачку и что даёт собранный отдел
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.4f, () => hub.tutorial.RunOnAlbum(this));
        }

        public override void Refresh()
        {
            var app = GameApp.I;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool open = app.DepartmentOpen(i);
                tabs[i].interactable = open;
                tabIcons[i].sprite = ArtLibrary.S(MetaCatalog.StickersOf(i)[0]);
                tabIcons[i].color = open ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
                // выбранная закладка выдвинута из-под страниц и ярче, остальные чуть задвинуты
                bool on = i == _dept;
                var c = open ? TabColors[i % TabColors.Length] : new Color(0.66f, 0.63f, 0.6f);
                tabs[i].image.color = on ? c : Color.Lerp(c, new Color(0.55f, 0.5f, 0.45f), 0.25f);
                var tr = (RectTransform)tabs[i].transform;
                tr.anchoredPosition = new Vector2(_tabX[i] + (on ? 0f : TabTucked), tr.anchoredPosition.y);
                tr.localScale = Vector3.one;
            }
            var list = MetaCatalog.StickersOf(_dept);
            for (int i = 0; i < cells.Length; i++) cells[i].Set(list[i], app.Save.stickers.Contains(list[i]), false);
            string gold = MetaCatalog.GoldId(_dept);
            goldCell.Set(gold, app.Save.goldStickers.Contains(gold), true);
            int owned = app.StickersOwned(_dept);
            deptTitle.text = MetaCatalog.DepartmentNames[_dept];
            progressText.text = $"{owned}/9";
            progressFill.fillAmount = owned / 9f;
            deptRewardDone.SetActive(app.Save.departmentsRewarded.Contains(_dept));
            if (deptRewardIcon != null)
            {
                deptRewardIcon.sprite = ArtLibrary.S(MetaCatalog.DeptFrames[_dept]) ?? ArtLibrary.S("icon_gift");
                deptRewardIcon.preserveAspect = true;
            }
            if (bonusText != null)
                bonusText.text = $"Собран отдел — +{Core.Economy.AlbumDeptBonusPct} % монет за уровни навсегда. Сейчас: +{app.AlbumBonusPct} %";

            int pending = app.Save.pendingPacks;
            openPackVideo.SetActive(pending == 0);
            openPackText.text = pending > 0 ? $"Открыть пачку ({pending})" : "Пачка за видео";
            openPackButton.interactable = pending > 0 || app.CanWatchPackAd;
        }

        void OnOpenPack()
        {
            var app = GameApp.I;
            if (app.Save.pendingPacks > 0) { hub.packPopup.OpenPending(hub, Refresh); return; }
            if (!app.CanWatchPackAd) { Toast.Show("На сегодня пачки за видео закончились"); return; }
            Platform.ShowRewarded("reward_pack", () =>
            {
                app.Save.packsOpenedToday++;
                app.Save.pendingPacks++;
                app.MarkDirty();
                hub.packPopup.OpenPending(hub, Refresh);
            });
        }
    }
}

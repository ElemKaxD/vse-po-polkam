using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Карта уровней (кадр 10): по одному району на экран, стрелки листают районы с плавной сменой
    /// (стыки сегментов не видны). 200 ценников лежат в сцене, енот стоит на текущем уровне.
    /// </summary>
    public class MapScreen : HubScreen
    {
        public RectTransform[] pages = new RectTransform[0];
        public Button prevButton, nextButton;
        public TextMeshProUGUI districtTitle;
        public MapNode[] nodes = new MapNode[0];
        public RectTransform[] districtLocks = new RectTransform[0];
        public RectTransform avatar;
        public Image avatarImage;
        public Image avatarHalo;
        public Sprite tagNormal, tagHard, tagSuper, tagRevision, tagLocked, starOn, starOff;

        [Header("Кнопки режимов")]
        public Button rushButton;
        public Button dailyButton;
        public Button tipsButton;
        public TextMeshProUGUI tipsText;
        public Button starChestButton;       // «Звёздный путь» (объект назван по прежнему сундуку — его место задал пользователь)
        public TextMeshProUGUI starTrackText;
        public GameObject starTrackDot;
        [OptionalRef] public Button modeButton;  // режим сложности (v4): перчик слева от ленты района
        [OptionalRef] public Image modeIcon;

        int _page = -1;

        void Awake()
        {
            prevButton.onClick.AddListener(() => ShowPage(_page - 1, true));
            nextButton.onClick.AddListener(() => ShowPage(_page + 1, true));
            foreach (var n in nodes)
            {
                var node = n;
                node.button.onClick.AddListener(() => OnNode(node.levelId));
            }
            rushButton.onClick.AddListener(() => GameApp.I.PlayRush());
            dailyButton.onClick.AddListener(() => hub.Show(hub.dailyScreen));
            tipsButton.onClick.AddListener(() => Platform.ShowRewarded("reward_tips", () =>
            {
                GameApp.I.ClaimTips();
                hub.FlyCoinsToTop(tipsButton.transform.position, 6);
                Refresh();
            }));
            starChestButton.onClick.AddListener(() => hub.OpenStarTrack());
            if (modeButton != null) modeButton.onClick.AddListener(() => { AudioService.Play("sfx_button"); hub.diffPopup.Open(hub); });
        }

        public override void Refresh()
        {
            var app = GameApp.I;
            foreach (var n in nodes) n.Refresh(app, tagNormal, tagHard, tagSuper, tagRevision, tagLocked, starOn, starOff);
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.35f, () => hub.tutorial.RunOnMap(this));

            // замки районов: пока не отремонтирован предыдущий
            for (int d = 2; d <= LevelPlanner.DistrictCount && d - 2 < districtLocks.Length; d++)
                districtLocks[d - 2].gameObject.SetActive(app.Mode == 0 && !app.StageComplete(d - 1) && app.Save.maxReached >= LevelPlanner.DistrictEnd(d - 1));

            // надетая косметика: скин енота и ореол вокруг него
            if (avatarImage != null)
            {
                avatarImage.sprite = app.FaceSprite(raccoonOnly: true);
                avatarImage.preserveAspect = true;
            }
            // ореол (венок) на карте не показываем: вокруг метки-енота он закрывал соседние ценники.
            // Надетый ореол виден на аватарке профиля вверху (HubController.profileHalo) — просьба 29.09.2026
            if (avatarHalo != null) avatarHalo.gameObject.SetActive(false);

            int cur = Mathf.Clamp(app.CurrentLevel, 1, nodes.Length);
            var node = nodes[cur - 1];
            // текущий уровень «дышит» мягким ореолом под ценником (эффект Arcaidia, 27.09.2026)
            if (_pulse != null) _pulse.Stop();
            _pulse = Vfx.Play("vfx_node_pulse", node.transform.position, 260f, parent: node.transform.parent);
            if (_pulse != null) _pulse.transform.SetSiblingIndex(node.transform.GetSiblingIndex());
            avatar.SetParent(node.transform.parent, false);
            avatar.anchoredPosition = ((RectTransform)node.transform).anchoredPosition + new Vector2(0f, 88f);
            avatar.SetAsLastSibling();
            Tween.Run(avatar, Tween.ChScale, 1000f, Ease.Linear, k =>
            {
                if (avatar != null) avatar.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(k * 1000f * 2.5f)) * 0.08f);
            });

            // затемнение закрытого района — поверх всего, включая метку-енота
            foreach (var l in districtLocks)
                if (l != null && l.gameObject.activeSelf) l.SetAsLastSibling();

            // при открытии — район текущего уровня
            ShowPage(LevelPlanner.DistrictOf(cur) - 1, false);

            rushButton.gameObject.SetActive(app.RushUnlocked);
            dailyButton.gameObject.SetActive(app.DailyUnlocked);
            bool tips = app.TipsAvailable && app.Save.maxReached >= Economy.TipsAt && app.BlockedByRenovation;
            tipsButton.gameObject.SetActive(tips);
            if (tips) tipsText.text = "Чаевые +" + Economy.TipsReward;
            RefreshStarTrack(app);
            if (modeButton != null)
            {
                modeButton.gameObject.SetActive(app.ModesAvailable);
                modeIcon.sprite = ArtLibrary.S(GameApp.ModeIcons[app.Mode]);
                modeIcon.preserveAspect = true;
            }
        }

        VfxPlayer _pulse;

        public const int StarTrackTutorialBit = 31;

        public void RefreshStars() { if (GameApp.I != null) RefreshStarTrack(GameApp.I); }

        /// <summary>
        /// Кнопка «Звёздного пути»: сколько звёзд из нужных до следующей награды, точка — есть что забрать.
        /// Пока енот не показал её (шаг обучения 31), кнопка тёмная и не нажимается, как закрытые кнопки меню —
        /// раньше она мигала уже после 1-го уровня, до обучения (03.10.2026).
        /// </summary>
        void RefreshStarTrack(GameApp app)
        {
            int next = app.NextStarReward;
            int ready = app.StarRewardsReady;
            bool taught = app.Save.Tutorial(StarTrackTutorialBit);
            starTrackText.text = next < 0 ? app.Save.StarsTotal.ToString() : $"{app.Save.StarsTotal}/{MetaCatalog.StarTrack[next].Stars}";
            starTrackDot.SetActive(ready > 0 && taught);
            starChestButton.interactable = taught;
            // как закрытые кнопки меню (HubController.Gate): серая и полупрозрачная
            if (starChestButton.image != null) starChestButton.image.color = taught ? Color.white : new Color(0.62f, 0.58f, 0.55f, 1f);
            var cg = starChestButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = starChestButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = taught ? 1f : 0.6f;
            var tr = starChestButton.transform;
            Tween.Kill(tr, Tween.ChScale);
            tr.localScale = Vector3.one;
            if (ready > 0 && taught)
                Tween.Run(tr, Tween.ChScale, 1000f, Ease.Linear, k =>
                {
                    if (tr != null) tr.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(k * 1000f * 3f)) * 0.08f);
                });
        }

        /// <summary>Самый дальний район, который можно посмотреть: текущий и следующий за ним.</summary>
        int MaxPage => Mathf.Min(pages.Length - 1, LevelPlanner.DistrictOf(Mathf.Clamp(GameApp.I.CurrentLevel, 1, nodes.Length)));

        void ShowPage(int page, bool animate)
        {
            page = Mathf.Clamp(page, 0, MaxPage);
            if (page == _page && animate) return;
            int old = _page;
            _page = page;
            for (int i = 0; i < pages.Length; i++)
            {
                bool on = i == page || (animate && i == old);
                pages[i].gameObject.SetActive(on);
            }
            var cg = pages[page].GetComponent<CanvasGroup>();
            if (animate && old >= 0 && old != page)
            {
                // новый район проявляется поверх старого с лёгким приближением
                pages[page].SetAsLastSibling();
                cg.alpha = 0f;
                pages[page].localScale = Vector3.one * 1.06f;
                Tween.Fade(cg, 1f, 0.35f, () => { if (old >= 0 && old != _page) pages[old].gameObject.SetActive(false); });
                Tween.Scale(pages[page], Vector3.one, 0.35f);
            }
            else { cg.alpha = 1f; pages[page].localScale = Vector3.one; }

            var store = MetaCatalog.StoreOf(page + 1);
            districtTitle.text = $"Район {page + 1} · {store.Name}";
            prevButton.gameObject.SetActive(page > 0);
            nextButton.gameObject.SetActive(page < MaxPage);
        }

        public override void Close()
        {
            Tween.Kill(avatar);
            base.Close();
        }

        void OnNode(int id)
        {
            var app = GameApp.I;
            if (id <= app.Save.Reached(app.Mode))   // пройден в текущем режиме сложности
            {
                hub.confirmPopup.Ask($"Уровень {id}", "Переиграть уровень? Звёзды обновятся, монет — 30%.", "icon_retry",
                    () => app.PlayLevel(id, PlayMode.Replay));
                return;
            }
            if (!app.CanPlay(id))
            {
                hub.confirmPopup.Ask("Сначала ремонт", "Закончи ремонт магазина, чтобы открыть новый район.", "icon_hammer",
                    () => hub.Show(hub.renovationScreen));
                return;
            }
            app.PlayLevel(id);
        }
    }
}

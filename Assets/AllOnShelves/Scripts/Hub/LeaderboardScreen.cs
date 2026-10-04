using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.LB;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Рейтинг по концепту пользователя (01.10.2026): «Сегодня», «Неделя», «Рядом», «Весь мир», «Награды».
    /// Каждая вкладка — своя картинка-страница (Art/Leaderboard/lb_page_N): ларёк с пьедесталом и пустым полем,
    /// игра кладёт сверху зверьков, ники, очки и подарки. Таблицы day/week с «вшитым» номером периода — League.
    /// Дизайн и награды — Docs/PROMPTS_Рейтинг_ВсёПоПолкам_01-10.txt.
    /// </summary>
    public class LeaderboardScreen : HubScreen
    {
        public const int TabCount = 5;
        public static readonly string[] TabNames = { "Сегодня", "Неделя", "Рядом", "Весь мир", "Награды" };
        static readonly string[] Boards = { League.DayBoard, League.WeekBoard, League.WeekBoard, League.WorldBoard, null };

        public GameObject[] pages = new GameObject[TabCount];
        public Button[] tabs = new Button[TabCount];
        public LeaderList[] lists = new LeaderList[4];      // вкладки 1–4
        public TextMeshProUGUI tabTitle;                    // табличка справа: какая вкладка и что считает
        [OptionalRef] public RectTransform activeTabCaption; // ленточка под активной вкладкой (v4)
        [OptionalRef] public TextMeshProUGUI activeTabText;
        public TextMeshProUGUI tabInfo;

        /// <summary>Что считает каждая вкладка (просьба 03.10.2026: было непонятно, где ты и за что очки).</summary>
        static readonly string[] TabInfo =
        {
            "Очки за сегодня: новый уровень +10, каждая звезда +5. Итоги — в полночь по Москве.",
            "Те же очки, но за всю неделю. Итоги — в ночь на понедельник.",
            "Ты и соседи по очкам недели — кого обогнать прямо сейчас.",
            "Все звёзды за всю игру. Эта таблица не сбрасывается.",
            "Награды за места в «Сегодня» и «Неделя». Итоги подвели — забирай здесь.",
        };
        public Button claimButton;                          // «Награды»: забрать итоги дня/недели
        public TextMeshProUGUI claimText;
        public RectTransform chest;                          // нарисованный сундук — из него «вылетает» награда

        int _tab;
        float _nextTimer;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => { if (_tab != t) { _tab = t; Refresh(); } });
            }
            claimButton.onClick.AddListener(OnClaim);
        }

        void OnEnable() { YG2.onGetLeaderboard += OnData; }
        void OnDisable() { YG2.onGetLeaderboard -= OnData; }

        /// <summary>Открыть рейтинг на вкладке (4 — «Награды», туда ведёт окно итогов).</summary>
        public void OpenTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabCount - 1);
            if (IsOpen) Refresh();
            else hub.Show(this);
        }

        public override void Close()
        {
            base.Close();
            _tab = 0;
        }

        public override void Open()
        {
            base.Open();
            // обучение рейтингу при первом входе (03.10.2026): вкладки → твоя строка → награды
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.8f, () => hub.tutorial.RunOnLeaders(this));
        }

        /// <summary>Строка игрока на открытой вкладке (для обучения).</summary>
        public RectTransform MyRowTarget =>
            _tab < lists.Length && lists[_tab] != null && lists[_tab].myRow != null && lists[_tab].myRow.gameObject.activeInHierarchy
                ? (RectTransform)lists[_tab].myRow.transform : null;

        void Update()
        {
            if (Time.unscaledTime < _nextTimer) return;
            _nextTimer = Time.unscaledTime + 20f;
            var week = lists.Length > 1 ? lists[1] : null;
            if (week != null && week.timer != null) week.timer.text = League.Left(League.WeekLeft);
            if (_tab == 4) RefreshClaim();
        }

        public override void Refresh()
        {
            League.Roll();
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == _tab);
            if (tabTitle != null) tabTitle.text = TabNames[_tab];
            if (activeTabCaption != null && _tab < tabs.Length && tabs[_tab] != null)
            {
                var p = activeTabCaption.anchoredPosition;
                activeTabCaption.anchoredPosition = new Vector2(((RectTransform)tabs[_tab].transform).anchoredPosition.x, p.y);
                activeTabText.text = TabNames[_tab];
            }
            if (tabInfo != null) tabInfo.text = TabInfo[_tab];
            if (_tab < lists.Length && lists[_tab] != null && lists[_tab].scroll != null) lists[_tab].scroll.verticalNormalizedPosition = 1f;
            _nextTimer = 0f;
            if (_tab == 4) { RefreshClaim(); return; }
            var list = lists[_tab];
            foreach (var r in list.rows) r.gameObject.SetActive(false);
            foreach (var p in list.podium) p.Set("", 0, null, null);
            if (list.myRow != null) list.myRow.gameObject.SetActive(false);
            list.status.gameObject.SetActive(true);
            list.status.text = "Загрузка...";
            int top = _tab == 2 ? 7 : list.podium.Length + list.rows.Length;
            Platform.RequestLeaderboard(Boards[_tab], Mathf.Clamp(top, 1, 20), _tab == 2 ? 3 : 1);
        }

        // ------------------------------------------------------------------ таблица

        void OnData(LBData data)
        {
            if (data == null || _tab >= 4 || data.technoName != Boards[_tab]) return;
            var players = new List<LBPlayerData>();
            if (data.players != null)
                foreach (var p in data.players)
                    // заглушка SDK без данных приходит строкой «no data» с местом 0 — это не игрок
                    if (p != null && p.rank > 0 && p.name != "no data") players.Add(p);
#if UNITY_EDITOR
            // в редакторе таблиц Яндекса нет — показываем выдуманных игроков, чтобы видеть раскладку
            if (players.Count == 0) players = DemoPlayers(data.technoName, out data.currentPlayer);
#endif
            // ответ на служебный запрос места (1 игрок) — не наш: ждём полный список
            if (players.Count <= 2 && int.TryParse(data.entries, out int total) && total > players.Count) return;
            Fill(lists[_tab], data, players);
        }

        int Base => _tab == 0 ? League.DayBase : _tab <= 2 ? League.WeekBase : 0;
        int Period => _tab == 0 ? GameApp.I.Save.lbDay : GameApp.I.Save.lbWeek;

        /// <summary>Очки записи текущего периода; -1 — запись прошлого дня/недели (не показываем).</summary>
        int Points(int score)
        {
            if (Base == 0) return score;
            return score / Base == Period ? score % Base : -1;
        }

        string ScoreSprite => _tab == 3 ? "icon_star" : "lb_points";

        int GiftOf(int rank, int pts)
        {
            if (_tab == 3) return 0;
            var r = _tab == 0 ? League.DayReward(rank) : League.WeekReward(rank, pts);
            return r.Empty ? 0 : r.Gift;
        }

        void Fill(LeaderList list, LBData data, List<LBPlayerData> players)
        {
            var s = GameApp.I.Save;
            int myRank = data.currentPlayer != null ? data.currentPlayer.rank : 0;
            int myPts = data.currentPlayer != null && myRank > 0 ? Points(data.currentPlayer.score) : -1;
            if (myPts < 0) { myRank = 0; myPts = _tab == 3 ? s.StarsTotal : _tab == 0 ? s.lbDayPts : s.lbWeekPts; }

            // только записи текущего периода, по порядку мест
            var cur = new List<LBPlayerData>();
            foreach (var p in players) if (Points(p.score) >= 0) cur.Add(p);
            cur.Sort((a, b) => a.rank.CompareTo(b.rank));

            int shown = 0;
            if (_tab == 2)
            {
                // «Рядом»: три выше игрока, он сам, три ниже; игрок в первой семёрке или его нет — первые семь
                int from = myRank > 4 ? myRank - 3 : 1;
                foreach (var p in cur)
                {
                    if (p.rank < from || shown >= list.rows.Length) continue;
                    bool me = myRank > 0 && p.rank == myRank;
                    SetRow(list.rows[shown++], p, me ? LeaderRow.Me : p.rank <= 3 ? LeaderRow.Top : LeaderRow.Normal);
                }
            }
            else
            {
                foreach (var p in cur)
                {
                    if (p.rank <= list.podium.Length)
                    {
                        League.Parse(p.extraData, p.uniqueID, out var av, out _, out var nick);
                        list.podium[p.rank - 1].Set(League.NameOf(p.name, nick), Points(p.score), av, ScoreSprite);
                        shown++;
                    }
                    else if (p.rank - list.podium.Length - 1 < list.rows.Length)
                    {
                        bool me = myRank > 0 && p.rank == myRank;
                        SetRow(list.rows[p.rank - list.podium.Length - 1], p, me ? LeaderRow.Me : LeaderRow.Normal);
                        shown++;
                    }
                }
            }

            // строка игрока — снизу, под разделителем (если он в списке выше, она ещё и подсвечена там)
            bool myShown = list.myRow != null && (myRank > 0 || myPts > 0);
            if (myShown)
                list.myRow.Set(myRank, ProfilePopup.DisplayName(), myPts, LeaderRow.Me, League.MyAvatarSprite, League.MyFrameSprite,
                               ScoreSprite, myRank > 0 ? GiftOf(myRank, myPts) : 0);
            // место выросло с прошлого раза — зелёная стрелка и блеск по строке (помним у себя в браузере)
            if (myShown && myRank > 0)
            {
                string key = "aos_lb_rank_" + _tab;
                int was = 0;
                try { was = PlayerPrefs.GetInt(key, 0); PlayerPrefs.SetInt(key, myRank); } catch { }
                if (was > 0 && myRank < was) Vfx.At("vfx_rank_up", list.myRow.transform, ((RectTransform)list.myRow.transform).rect.width, delay: 0.3f);
            }

            // статус стоит на месте строки игрока: виден, когда её нет (или таблица пуста на «Рядом»)
            list.status.gameObject.SetActive(list.myRow != null ? !myShown : shown == 0);
            list.status.text = shown == 0 ? "Пока пусто — сыграй уровень и будь первым!" : "Сыграй уровень, чтобы попасть в таблицу";
            if (list.timer != null) list.timer.text = League.Left(League.WeekLeft);
        }

        void SetRow(LeaderRow row, LBPlayerData p, Color bg)
        {
            League.Parse(p.extraData, p.uniqueID, out var av, out var fr, out var nick);
            int pts = Points(p.score);
            row.Set(p.rank, League.NameOf(p.name, nick), pts, bg, av, fr, ScoreSprite, GiftOf(p.rank, pts));
        }

        // ------------------------------------------------------------------ награды

        void RefreshClaim()
        {
            bool has = League.HasPending;
            claimButton.interactable = has;
            claimText.text = has ? "Забрать награду" : "Итоги дня — через " + League.Left(League.DayLeft);
        }

        void OnClaim()
        {
            if (!League.HasPending) return;
            ClaimWithPopup(hub, chest != null ? chest : (RectTransform)claimButton.transform, RefreshClaim);
        }

        /// <summary>Окно «Итоги недели: 7-е место!» → «Забрать» (и с карты, и со страницы наград).</summary>
        public static void ClaimWithPopup(HubController hub, RectTransform from, System.Action done)
        {
            hub.confirmPopup.Info(League.PendingTitle(), League.PendingText(), "lb_claim_chest_open", "Забрать", () =>
            {
                int coins = League.ClaimAll();
                GameApp.I.Flush();
                AudioService.Play("sfx_chest_open");
                if (from != null) Vfx.At("vfx_chest_open", from, 420f);
                if (coins > 0) hub.FlyCoinsToTop(from != null ? from.position : hub.transform.position, 6);
                hub.RefreshTop();
                if (GameApp.I.Save.pendingPacks > 0) hub.packPopup.OpenPending(hub, done);
                else done?.Invoke();
            });
        }

#if UNITY_EDITOR
        static readonly string[] DemoNames =
        {
            "Маша_Полочкина", "Котофей", "Ivan Petrov", "СуперБабушка2000", "Лиса Алиса", "anonymous", "Петя", "ОченьДлинноеИмяИгрокаКотороеНеВлезает",
            "Ёжик в тумане", "Dasha", "Гость", "Продавец года", "Мурка", "Алексей", "Ника", "Хозяйка лавки",
        };

        /// <summary>Выдуманная таблица для редактора: двадцать игроков текущего периода, игрок — пятый.</summary>
        List<LBPlayerData> DemoPlayers(string board, out LBCurrentPlayerData me)
        {
            var list = new List<LBPlayerData>();
            string[] av = { "cat", "corgi", "bear", "bunny", "penguin", "fox", "panda", "hedgehog", "owl", "frog", "pig", "raccoon" };
            string[] fr = { "crown", "silver", "bronze", "laurel", "", "", "laurel", "" };
            int baseScore = Base == 0 ? 0 : Period * Base;
            for (int i = 0; i < 20; i++)
            {
                int pts = Base == 0 ? 330 - i * 13 : 2400 - i * 97;
                list.Add(new LBPlayerData
                {
                    rank = i + 1, name = DemoNames[i % DemoNames.Length], score = baseScore + pts, uniqueID = "demo" + i,
                    extraData = i % 3 == 2 ? null : $"a:{av[i % av.Length]}|f:{fr[i % fr.Length]}|n:",
                });
            }
            me = new LBCurrentPlayerData { rank = 5, score = list[4].score, extraData = League.Extra() };
            list[4].name = ProfilePopup.DisplayName();
            list[4].extraData = League.Extra();
            return list;
        }
#endif
    }
}

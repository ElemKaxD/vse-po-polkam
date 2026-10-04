using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Экран «Задания» (03.10.2026, картинка quests_empty — пробковая доска): слева 3 листочка заданий дня,
    /// справа сверху — шкала недели из 5 сундуков, снизу — лента месяца с большим призом. Логика — Services/Quests.
    /// </summary>
    public class QuestsScreen : HubScreen
    {
        public Image[] noteIcons = new Image[3];
        public TextMeshProUGUI[] noteTexts = new TextMeshProUGUI[3];
        public Image[] noteFills = new Image[3];
        public TextMeshProUGUI[] noteProgress = new TextMeshProUGUI[3];
        public Button[] noteClaim = new Button[3];
        public GameObject[] noteDone = new GameObject[3];

        public Button[] weekChests = new Button[5];
        public GameObject[] weekChecks = new GameObject[5];
        public GameObject[] weekDots = new GameObject[5];
        public Image weekFill;
        public TextMeshProUGUI weekText;
        public float[] weekX = new float[5];      // где стоят сундуки (для заливки полосы)
        public float weekX0, weekX1;

        public Image[] monthIcons = new Image[5];
        public Button[] monthButtons = new Button[5];
        public GameObject[] monthChecks = new GameObject[5];
        public GameObject[] monthDots = new GameObject[5];
        public Image monthFill;
        public TextMeshProUGUI monthText;
        public float[] monthX = new float[5];
        public float monthX0, monthX1;

        public TextMeshProUGUI dayText;

        void Awake()
        {
            for (int i = 0; i < 3; i++) { int k = i; noteClaim[i].onClick.AddListener(() => ClaimQuest(k)); }
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                weekChests[i].onClick.AddListener(() => ClaimWeek(k));
                monthButtons[i].onClick.AddListener(() => ClaimMonth(k));
            }
        }

        public override void Open()
        {
            base.Open();
            Quests.Changed += Refresh;
        }

        readonly VfxPlayer[] _weekGlow = new VfxPlayer[5], _monthGlow = new VfxPlayer[5];

        public override void Close()
        {
            for (int i = 0; i < 5; i++) { Vfx.Keep(ref _weekGlow[i], false, null); Vfx.Keep(ref _monthGlow[i], false, null); }
            Quests.Changed -= Refresh;
            base.Close();
        }

        float _tick;
        void Update()
        {
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            dayText.text = "Новые задания через " + League.Left(League.DayLeft);
        }

        public override void Refresh()
        {
            if (GameApp.I == null) return;
            Quests.Roll();
            for (int i = 0; i < 3; i++)
            {
                var d = Quests.Get(i);
                if (d == null) continue;
                noteIcons[i].sprite = ArtLibrary.S(d.Icon);
                noteIcons[i].preserveAspect = true;
                noteTexts[i].text = Quests.TextOf(i);
                int p = Quests.Progress(i);
                noteFills[i].fillAmount = p / (float)d.Target;
                noteProgress[i].text = $"{p} / {d.Target}";
                bool claimed = Quests.Claimed(i);
                noteDone[i].SetActive(claimed);
                noteClaim[i].gameObject.SetActive(!claimed);
                noteClaim[i].interactable = Quests.Done(i);
            }
            for (int i = 0; i < 5; i++)
            {
                bool ready = Quests.WeekReady(i), got = Quests.WeekClaimed(i);
                weekChecks[i].SetActive(got);
                weekDots[i].SetActive(ready);
                weekChests[i].image.color = got ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
                Pulse(weekChests[i].transform, ready);
                int k = i;
                Vfx.Keep(ref _weekGlow[i], ready && gameObject.activeInHierarchy, () => Vfx.Behind("vfx_chest_ready", weekChests[k].transform, 200f));
                bool mReady = Quests.MonthReady(i), mGot = Quests.MonthClaimed(i);
                monthChecks[i].SetActive(mGot);
                monthDots[i].SetActive(mReady);
                monthIcons[i].color = mGot ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
                Pulse(monthButtons[i].transform, mReady);
                Vfx.Keep(ref _monthGlow[i], mReady && gameObject.activeInHierarchy, () => Vfx.Behind("vfx_chest_ready", monthButtons[k].transform, 180f));
            }
            weekFill.fillAmount = Along(Quests.WeekPts, Quests.WeekPoints, weekX, weekX0, weekX1);
            monthFill.fillAmount = Along(Quests.MonthPts, Quests.MonthPoints, monthX, monthX0, monthX1);
            weekText.text = $"Неделя: {Quests.WeekPts} / {Quests.WeekPoints[4]} · ещё {League.Left(League.WeekLeft)}";
            monthText.text = $"Месяц: {Quests.MonthPts} / {Quests.MonthPoints[4]}";
            hub?.RefreshTop();
        }

        /// <summary>Доля полосы: очки ставятся на отрезки между сундуками (они стоят не через равные очки).</summary>
        static float Along(int pts, int[] at, float[] xs, float x0, float x1)
        {
            float x = x0;
            int prev = 0;
            float px = x0;
            for (int i = 0; i < at.Length; i++)
            {
                if (pts >= at[i]) { x = xs[i]; prev = at[i]; px = xs[i]; continue; }
                x = Mathf.Lerp(px, xs[i], (pts - prev) / (float)Mathf.Max(1, at[i] - prev));
                break;
            }
            if (pts >= at[at.Length - 1]) x = x1;
            return Mathf.Clamp01((x - x0) / Mathf.Max(1f, x1 - x0));
        }

        static void Pulse(Transform t, bool on)
        {
            Tween.Kill(t);
            t.localScale = Vector3.one;
            if (!on) return;
            Tween.Run(t, Tween.ChScale, 1000f, Ease.Linear, k =>
            {
                if (t != null) t.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(k * 1000f * 3f)) * 0.07f);
            });
        }

        void ClaimQuest(int i)
        {
            if (!Quests.Claim(i)) { AudioService.Play("sfx_nope"); return; }
            AudioService.Play("sfx_coins_fly");
            hub?.FlyCoinsToTop(noteClaim[i].transform.position, 5);
            Vfx.At("vfx_quest_done", noteIcons[i].transform, 260f);
            Vfx.At("vfx_quest_token_fly", noteClaim[i].transform, 420f, delay: 0.15f);
            Refresh();
        }

        void ClaimWeek(int i)
        {
            if (!Quests.ClaimWeek(i))
            {
                AudioService.Play("sfx_button");
                Toast.Show($"Сундук откроется на {Quests.WeekPoints[i]} листиках — выполняй задания", "quest_token");
                return;
            }
            Opened(weekChests[i].transform, Quests.WeekRewards[i], i == 4 ? Quests.WeekGoldCoins : 0);
        }

        void ClaimMonth(int i)
        {
            if (!Quests.ClaimMonth(i))
            {
                AudioService.Play("sfx_button");
                Toast.Show($"Награда месяца — на {Quests.MonthPoints[i]} листиках", "quest_token");
                return;
            }
            if (i == 4) Vfx.At("vfx_month_prize", monthButtons[i].transform, 760f);
            Opened(monthButtons[i].transform, Quests.MonthRewards[i], i == 4 ? Quests.MonthPrizeCoins : 0);
        }

        void Opened(Transform at, StarReward r, int extraCoins)
        {
            AudioService.Play("sfx_chest_open");
            Vfx.At("vfx_chest_open", at, 300f);
            Toast.Show("Получено: " + r.Title + (extraCoins > 0 ? $" и {extraCoins} монет" : ""), r.Icon);
            if (r.Kind == "coins" || extraCoins > 0) hub?.FlyCoinsToTop(at.position, 6);
            Refresh();
            // пачку наклеек — сразу открыть, как в «Звёздном пути»
            if (r.Kind == "pack" && hub != null && GameApp.I.Save.maxReached >= Core.Economy.AlbumAt) hub.packPopup.OpenPending(hub, Refresh);
        }
    }
}

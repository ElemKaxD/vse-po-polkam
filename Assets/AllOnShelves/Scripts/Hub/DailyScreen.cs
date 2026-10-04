using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// «Завоз дня» v4 (03.10.2026, картинка daily_empty): доска-календарь на 7 дней (6 ящиков и сундук 7-го дня),
    /// сегодняшний ящик светится, под доской — «Забрать» и «Ещё ящик» за видео. Справа — карточка «Испытание дня»:
    /// правило дня, сундук-награда, 7 кружков «дней подряд», «Играть». Логика наград — GameApp.ClaimDaily, Challenge.
    /// </summary>
    public class DailyScreen : HubScreen
    {
        public Image[] crates = new Image[7];
        public TextMeshProUGUI[] dayTexts = new TextMeshProUGUI[7];
        public TextMeshProUGUI[] rewardTexts = new TextMeshProUGUI[7];
        public GameObject[] glows = new GameObject[7];
        public Sprite crateClosed, crateOpen, crateDone, chestBig;
        public Button claimButton;
        public TextMeshProUGUI claimText;
        public Button chest2Button;

        public RectTransform challengeCard;
        public Image ruleIcon;
        [OptionalRef] public TextMeshProUGUI ruleNum;   // цифра в кольце значка правила («3 звезды»)
        public TextMeshProUGUI challengeTitle;
        public TextMeshProUGUI ruleText;
        public Image challengeChest;
        public Image[] dots = new Image[7];
        public Sprite dotOn, dotOff;
        public Button playButton;
        public TextMeshProUGUI playText;

        void Awake()
        {
            claimButton.onClick.AddListener(OnClaim);
            chest2Button.onClick.AddListener(() => Platform.ShowRewarded("reward_chest2", () =>
            {
                GameApp.I.ClaimDaily(0.5f);
                AudioService.Play("sfx_chest_open");
                Vfx.At("vfx_chest_open", chest2Button.transform, 420f);
                hub.FlyCoinsToTop(chest2Button.transform.position, 5);
                Refresh();
            }));
            playButton.onClick.AddListener(() =>
            {
                if (!Challenge.Unlocked || Challenge.WonToday) { AudioService.Play("sfx_nope"); return; }
                AudioService.Play("sfx_button");
                GameApp.I.PlayLevel(0, PlayMode.Daily);
            });
        }

        public override void Open()
        {
            base.Open();
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.6f, () => hub.tutorial.RunOnDaily(this));
        }

        public override void Refresh()
        {
            var app = GameApp.I;
            var s = app.Save;
            bool canClaim = app.CanClaimDaily;
            int today = s.streakIndex;
            for (int i = 0; i < crates.Length; i++)
            {
                bool claimed = i < today || (!canClaim && today == 0 && s.lastClaimDate == GameApp.Today);
                bool isToday = canClaim && i == today;
                crates[i].sprite = i == 6 ? chestBig : claimed ? crateDone : crateClosed;
                crates[i].preserveAspect = true;
                crates[i].color = claimed && i == 6 ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
                dayTexts[i].text = isToday ? "Сегодня" : $"День {i + 1}";
                rewardTexts[i].text = app.DailyRewardText(i);
                glows[i].SetActive(isToday);
                var tr = crates[i].transform;
                if (isToday)
                    Tween.Run(tr, Tween.ChRot, 1000f, Ease.Linear, k =>
                    {
                        if (tr != null) tr.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 1000f * 6f) * 4f);
                    });
                else { Tween.Kill(tr, Tween.ChRot); tr.localRotation = Quaternion.identity; }
            }
            claimButton.interactable = canClaim;
            claimText.text = !app.DailyUnlocked ? "С уровня " + Core.Economy.DailyAt : canClaim ? "Забрать" : "Новый ящик через " + League.Left(League.DayLeft);
            chest2Button.gameObject.SetActive(app.CanClaimChest2);

            // Испытание дня
            int r = (int)Challenge.Today;
            ruleIcon.sprite = ArtLibrary.S(Challenge.Icons[r]);
            ruleIcon.preserveAspect = true;
            if (ruleNum != null)
            {
                string num = Challenge.IconNumber(Challenge.Today);
                ruleNum.gameObject.SetActive(num != null);
                ruleNum.text = num ?? "";
            }
            challengeTitle.text = Challenge.Titles[r];
            ruleText.text = Challenge.WonToday ? "Пройдено! Новое испытание завтра." : Challenge.Texts[r];
            int run = Challenge.Run % Challenge.RunDays;
            if (run == 0 && Challenge.Run > 0 && Challenge.WonToday) run = Challenge.RunDays;   // сегодня закрыл неделю — все кружки горят
            for (int i = 0; i < dots.Length; i++) dots[i].sprite = i < run ? dotOn : dotOff;
            // новый кружок «подряд» вспыхивает один раз за запуск игры
            if (Challenge.WonToday && run > 0 && _dotShown != Challenge.Run)
            {
                _dotShown = Challenge.Run;
                Vfx.At("vfx_daily_dot_fill", dots[run - 1].transform, 150f, delay: 0.4f);
            }
            challengeChest.color = Challenge.WonToday ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            playButton.interactable = Challenge.Unlocked && !Challenge.WonToday;
            playText.text = !Challenge.Unlocked ? "С уровня " + Core.Economy.DailyAt : Challenge.WonToday ? "Пройдено" : "Играть";
        }

        static int _dotShown = -1;

        void OnClaim()
        {
            var app = GameApp.I;
            if (!app.CanClaimDaily) return;
            int idx = app.Save.streakIndex;
            app.ClaimDaily();
            app.Flush();
            AudioService.Play("sfx_chest_open");
            // ящик открывается (крышка рядом), вспышка и монеты; через миг — штамп «получено»
            if (idx < 6) crates[idx].sprite = crateOpen;
            Tween.Punch(crates[idx].transform, 0.25f, 0.4f);
            Vfx.At("vfx_crate_open", crates[idx].transform, 420f);
            hub.FlyCoinsToTop(crates[idx].transform.position, 6);
            Toast.Show("Получено: " + app.DailyRewardText(idx), "icon_gift");
            Tween.Delay(this, 0.9f, Refresh);
            hub.RefreshTop();
            if (app.Save.pendingPacks > 0 && (idx == 2 || idx == 6)) Tween.Delay(this, 1.0f, () => hub.packPopup.OpenPending(hub, Refresh));
        }
    }
}

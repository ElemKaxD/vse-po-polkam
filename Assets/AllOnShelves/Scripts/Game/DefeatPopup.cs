using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Экран поражения (кадр 07): бесплатный откат к спасению, «Грузчик» за рекламу / монеты,
    /// «Заново», «На карту». Кнопки выстраиваются столбиком — сколько видно, столько и занимают места.
    /// </summary>
    public class DefeatPopup : Popup
    {
        public TextMeshProUGUI title;
        public TextMeshProUGUI reason;
        public Button freeUndoButton;
        public Button loaderAdButton;
        public Button loaderCoinsButton;
        public TextMeshProUGUI loaderCoinsText;
        public Button bonusRetryButton;
        public Button retryButton;
        public Button mapButton;
        [OptionalRef] public GameObject streakLine;     // «Серия ×N сгорит, если сдашься» (03.10.2026)
        [OptionalRef] public TextMeshProUGUI streakText;
        [OptionalRef] public Button keepStreakButton;   // «Сохранить серию» за 💎 8 или видео (экономика v4)
        [OptionalRef] public TextMeshProUGUI keepStreakText;
        [OptionalRef] public Image keepStreakIcon;

        /// <summary>Строка про серию побед: видна, пока серия есть (продолжение уровня её сохраняет).</summary>
        public void ShowStreak(int wins) => ShowStreak(wins, false, null);

        /// <summary>Плашка серии. onKeep != null — кнопка «Сохранить» (за алмазы; нет алмазов — за видео, если ещё можно).</summary>
        public void ShowStreak(int wins, bool kept, System.Action onKeep)
        {
            if (streakLine == null) return;
            streakLine.SetActive(wins > 0);
            if (wins > 0) streakText.text = kept ? $"Серия ×{wins} сохранена!" : $"Серия ×{wins} сгорит, если сдашься";
            if (keepStreakButton == null) return;
            bool show = wins >= Streak.KeepAt && !kept && onKeep != null;
            keepStreakButton.gameObject.SetActive(show);
            if (!show) return;
            bool gems = GameApp.I.Gems >= Core.Economy.GemStreakSave || !Streak.CanKeepByVideo;
            keepStreakText.text = gems ? $"Сохранить {Core.Economy.GemStreakSave}" : "Сохранить";
            keepStreakIcon.sprite = ArtLibrary.S(gems ? "icon_gem_small" : "icon_video");
            keepStreakIcon.preserveAspect = true;
            keepStreakButton.onClick.RemoveAllListeners();
            keepStreakButton.onClick.AddListener(() => onKeep());
        }

        public void Setup(string titleText, string reasonText, bool freeUndo, bool loaderAd, bool loaderCoins, int loaderPrice, bool bonusRetry,
                          Action onFreeUndo, Action onLoaderAd, Action onLoaderCoins, Action onBonusRetry, Action onRetry, Action onMap)
        {
            title.text = titleText;
            reason.text = reasonText;
            Bind(freeUndoButton, freeUndo, onFreeUndo);
            Bind(loaderAdButton, loaderAd, onLoaderAd);
            Bind(loaderCoinsButton, loaderCoins, onLoaderCoins);
            loaderCoinsText.text = "Грузчик +2 за " + loaderPrice;
            Bind(bonusRetryButton, bonusRetry, onBonusRetry);
            Bind(retryButton, true, onRetry);
            Bind(mapButton, true, onMap);
            StackButtons();
            retryButton.gameObject.SetActive(false);
            mapButton.gameObject.SetActive(false);
            Show();
            // «Заново» появляется через 1.5 c (ГДД 12.2)
            Tween.Delay(retryButton, 1.5f, () =>
            {
                retryButton.gameObject.SetActive(true);
                mapButton.gameObject.SetActive(true);
            });
        }

        /// <summary>Видимые кнопки — столбиком по центру, «Заново» и «На карту» — рядом под ними.</summary>
        void StackButtons()
        {
            float y = 20f;   // ниже надписи о причине: иначе первая кнопка налезает на вторую строку
            foreach (var b in new[] { freeUndoButton, loaderAdButton, loaderCoinsButton, bonusRetryButton })
            {
                if (b == null || !b.gameObject.activeSelf) continue;
                var rt = (RectTransform)b.transform;
                rt.anchoredPosition = new Vector2(0f, y);
                y -= rt.sizeDelta.y + 22f;
            }
            float row = y - 32f;
            ((RectTransform)retryButton.transform).anchoredPosition = new Vector2(-170f, row);
            ((RectTransform)mapButton.transform).anchoredPosition = new Vector2(175f, row);
        }

        static void Bind(Button b, bool visible, Action a)
        {
            b.gameObject.SetActive(visible);
            b.interactable = true;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => a?.Invoke());
        }
    }
}

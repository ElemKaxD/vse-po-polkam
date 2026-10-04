using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Экран победы (кадр 06): звёзды, монеты, ×2 за рекламу, «Дальше».</summary>
    public class VictoryPopup : Popup
    {
        public Image[] stars = new Image[3];
        public Sprite starOn, starOff;
        public TextMeshProUGUI title;
        public TextMeshProUGUI coinsText;
        public RectTransform raccoon;
        public Button doubleButton;          // обычная кнопка рекламы с подписью: «Подарок ×2», «×3»
        public Button doubleArtButton;       // готовая кнопка «▶ x2» целиком
        public TextMeshProUGUI doubleText;
        public GameObject doubleVideoIcon;
        public Button nextButton;
        public TextMeshProUGUI nextText;
        public Button mapButton;
        public Button retryButton;     // «заново»: переиграть уровень на три звезды (просьба 26.09.2026)
        public GameObject packHint;
        public TextMeshProUGUI trackText;     // «До награды за звёзды: ещё 3 звезды»
        [OptionalRef] public GameObject piggyRoot;       // копилка: «+35» под свинкой (03.10.2026)
        [OptionalRef] public TextMeshProUGUI piggyText;

        int _coins;

        /// <summary>Сколько упало в копилку; full — копилка полна (тогда «полна!» вместо числа).</summary>
        public void SetPiggy(int added, bool full)
        {
            if (piggyRoot == null) return;
            piggyRoot.SetActive(added > 0 || full);
            if (piggyText != null) piggyText.text = added > 0 ? "+" + added : "полна!";
            if (added > 0 || full)
            {
                piggyRoot.transform.localScale = Vector3.zero;
                Tween.Scale(piggyRoot.transform, Vector3.one, 0.35f, Ease.OutBack, null, 0.9f);
            }
            if (added > 0)
                Tween.Delay(piggyRoot, 1.3f, () => Vfx.At("vfx_piggy_coin_drop", piggyRoot.transform, 190f));
        }

        public void Setup(int starCount, int coins, int multiplier, bool freeGift, bool showDouble, bool packReady, string nextLabel,
                          Action onDouble, Action onNext, Action onMap = null, Action onRetry = null)
        {
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(onRetry != null);
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => onRetry?.Invoke());
            }
            _coins = coins;
            coinsText.text = "+" + coins;
            title.text = starCount == 3 ? "Идеально!" : starCount == 2 ? "Отлично!" : "Уровень пройден!";
            nextText.text = nextLabel;
            if (mapButton != null)
            {
                mapButton.gameObject.SetActive(onMap != null);
                mapButton.onClick.RemoveAllListeners();
                mapButton.onClick.AddListener(() => onMap?.Invoke());
            }
            packHint.SetActive(packReady);
            if (trackText != null)
            {
                string t = packReady ? "" : StarTrackLine();
                trackText.gameObject.SetActive(t.Length > 0);
                trackText.text = t;
            }
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].sprite = starOff;
                stars[i].transform.localScale = Vector3.one;
            }
            // «▶ x2» — одна и та же кнопка на всех уровнях (на 2-м тоже, просьба 22.09.2026)
            bool art = doubleArtButton != null && multiplier == 2;
            doubleButton.gameObject.SetActive(showDouble && !art);
            if (doubleArtButton != null) doubleArtButton.gameObject.SetActive(showDouble && art);
            doubleText.text = freeGift ? $"Подарок ×{multiplier}" : $"×{multiplier}";
            doubleVideoIcon.SetActive(!freeGift);
            foreach (var b in new[] { doubleButton, doubleArtButton })
            {
                if (b == null) continue;
                b.interactable = true;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => onDouble?.Invoke());
            }
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => onNext?.Invoke());
            nextButton.gameObject.SetActive(false);

            Show();
            for (int i = 0; i < starCount && i < stars.Length; i++)
            {
                int idx = i;
                Tween.Delay(stars[idx], 0.35f + 0.3f * idx, () =>
                {
                    stars[idx].sprite = starOn;
                    stars[idx].transform.localScale = Vector3.one * 1.6f;
                    Vfx.At("vfx_star_award", stars[idx].transform, 250f);
                    Tween.Scale(stars[idx].transform, Vector3.one, 0.3f, Ease.OutBack);
                    AudioService.Play("sfx_star_" + (idx + 1));
                });
            }
            Tween.Punch(raccoon, 0.12f, 0.5f);
            Tween.Delay(nextButton, 1.0f, () =>
            {
                nextButton.gameObject.SetActive(true);
                nextButton.transform.localScale = Vector3.zero;
                Tween.Scale(nextButton.transform, Vector3.one, 0.25f, Ease.OutBack);
            });
        }

        /// <summary>Строка «Звёздного пути» после уровня: звёзды должны куда-то вести.</summary>
        static string StarTrackLine()
        {
            var app = GameApp.I;
            if (app == null) return "";
            if (app.StarRewardsReady > 0) return "Награда за звёзды ждёт на карте!";
            int next = app.NextStarReward;
            if (next < 0) return "";
            int need = MetaCatalog.StarTrack[next].Stars - app.Save.StarsTotal;
            int m10 = need % 10, m100 = need % 100;
            string word = m10 == 1 && m100 != 11 ? "звезда" : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? "звезды" : "звёзд";
            return $"До награды «{MetaCatalog.StarTrack[next].Title}»: ещё {need} {word}";
        }

        public void ShowDoubled(int total)
        {
            doubleButton.interactable = false;
            doubleButton.gameObject.SetActive(false);
            if (doubleArtButton != null) doubleArtButton.gameObject.SetActive(false);
            int from = _coins;
            Tween.Run(coinsText, Tween.ChCustom, 0.6f, Ease.OutQuad, k =>
            {
                if (coinsText != null) coinsText.text = "+" + Mathf.RoundToInt(Mathf.Lerp(from, total, k));
            });
            Tween.Punch(coinsText.transform, 0.3f, 0.4f);
            AudioService.Play("sfx_coins_fly");
            Vfx.At("vfx_coin_fountain", coinsText.transform, 320f);
        }
    }
}

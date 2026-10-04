using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Бустер закончился (подсказка, отмена) или нужен дополнительный (полка): купить за монеты,
    /// получить за рекламу или взять набор за реальные деньги. Рекомендация издателя 29.09.2026:
    /// «бустеры за монеты / рекламу / IAP» — раньше при нуле сразу запускалась реклама.
    /// Экономика v2 (30.09.2026): то же окно показывает «Не хватает монет» (Shortage) — в уровне и в ремонте.
    /// </summary>
    public class BoosterPopup : Popup
    {
        public TextMeshProUGUI title;
        public Image picture;
        public TextMeshProUGUI text;
        public Button coinsButton;
        public TextMeshProUGUI coinsText;
        public Button adButton;
        public TextMeshProUGUI adText;
        public Button iapButton;
        public TextMeshProUGUI iapText;
        public Button closeButton;

        /// <summary>Что предлагаем. Цена ≤ 0 — за монеты не продаётся; iapId пустой — без покупки.</summary>
        public struct Offer
        {
            public string Title, Icon, Text;
            public int Price;
            public string CoinsLabel, AdLabel, IapId;
            public Action OnCoins, OnAd, OnIap, OnClose;
        }

        Vector2 _adPos;
        bool _posSaved;

        public void Open(Offer o)
        {
            title.text = o.Title;
            picture.sprite = ArtLibrary.S(o.Icon);
            ArtLibrary.Fit(picture, 170f);
            text.text = o.Text;

            var app = GameApp.I;
            bool canCoins = o.Price > 0 && o.OnCoins != null;
            coinsButton.gameObject.SetActive(canCoins);
            if (canCoins)
            {
                coinsText.text = o.CoinsLabel;
                // монет не хватает — кнопка всё равно нажимается: откроет «Не хватает монет» (видео или пакет),
                // а когда монеты придут, вернёт к этой же покупке
                coinsButton.interactable = true;
            }
            adButton.gameObject.SetActive(o.OnAd != null);
            adText.text = o.AdLabel;
            // без кнопки монет («Не хватает монет») видео встаёт посередине между двумя местами
            var adRt = (RectTransform)adButton.transform;
            var coinsRt = (RectTransform)coinsButton.transform;
            if (!_posSaved) { _posSaved = true; _adPos = adRt.anchoredPosition; }
            adRt.anchoredPosition = canCoins ? _adPos : new Vector2((_adPos.x + coinsRt.anchoredPosition.x) / 2f, _adPos.y);

            var product = string.IsNullOrEmpty(o.IapId) ? null : MetaCatalog.Product(o.IapId);
            iapButton.gameObject.SetActive(product != null && o.OnIap != null);
            if (product != null)
            {
                string price = Platform.PriceOf(product.Id);
                // две строки: название и цена — в одну строку на кнопке мельчило до нечитаемого
                iapText.text = product.Title + "\n" + (string.IsNullOrEmpty(price) ? product.FallbackPrice : price);
                // значок на кнопке: монеты — для пакета монет, подарок — для набора помощника
                var ic = iapButton.transform.Find("Icon")?.GetComponent<Image>();
                if (ic != null)
                {
                    ic.sprite = ArtLibrary.S(product.Coins > 0 && product.Undo == 0 ? "icon_coin" : "icon_gift");
                    ic.preserveAspect = true;
                }
            }

            Bind(coinsButton, () =>
            {
                if (app == null) return;
                if (app.TrySpend(o.Price)) { Hide(); o.OnCoins?.Invoke(); return; }
                AudioService.Play("sfx_nope");
                var s = Shortage(o.Price - app.Coins, () => Open(o));
                s.OnClose = o.OnClose;
                Open(s);
            });
            Bind(adButton, () => { Hide(); o.OnAd?.Invoke(); });
            Bind(iapButton, () => { Hide(); o.OnIap?.Invoke(); });
            Bind(closeButton, () => { Hide(); o.OnClose?.Invoke(); });
            Show();
        }

        /// <summary>
        /// «Не хватает монет»: монеты за видео (не больше Economy.AdCoinsPerDay раз в день) или самый маленький
        /// пакет монет, которого хватит. onGot — монеты пришли (например, вернуть к покупке).
        /// </summary>
        public static Offer Shortage(int need, Action onGot)
        {
            var app = GameApp.I;
            var pack = MetaCatalog.CoinPackFor(need);
            bool ad = app != null && app.CanWatchCoinsAd;
            return new Offer
            {
                Title = "Не хватает монет", Icon = "shop_coins_m",
                Text = ad ? $"Нужно ещё {need} монет. Посмотри видео или переиграй уровни на три звезды."
                          : $"Нужно ещё {need} монет. Переиграй уровни на три звезды или возьми монеты.",
                AdLabel = app != null ? $"+{app.AdCoinsAmount} за видео" : "",
                IapId = pack.Id,
                OnAd = ad ? () => Platform.ShowRewarded("reward_coins", () =>
                {
                    app.ClaimAdCoins();
                    AudioService.Play("sfx_coins_fly");
                    Toast.Show($"+{app.AdCoinsAmount} монет", "icon_coin");
                    onGot?.Invoke();
                }) : (Action)null,
                OnIap = () => Platform.Buy(pack.Id, _ => { AudioService.Play("sfx_purchase_success"); onGot?.Invoke(); }),
            };
        }

        static void Bind(Button b, Action a)
        {
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => a?.Invoke());
        }
    }
}

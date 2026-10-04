using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Карточка товара в магазине. Магазин по концепту (01.10.2026): карточка нарисована на странице, здесь —
    /// только содержимое поверх неё: название на табличке, картинка, цена на зелёной кнопке.
    /// productId — товар Яндекса или «свой» товар: ad_coins / ad_hint / ad_undo / ad_pack (за видео),
    /// buy_undo3 / buy_hint3 (за монеты).
    /// </summary>
    public class ShopCard : MonoBehaviour
    {
        public string productId;
        public string iconName;          // своя картинка (на подиуме — крупная); пусто — значок товара
        public string badge;             // «Хит», «Выгодно», «Новое» — ленточка в углу
        public bool Owned { get; private set; }
        public bool keepIcon;            // устарело (оформление показывалось превью уровня)
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI description;
        public Button buyButton;
        public TextMeshProUGUI priceText;
        [OptionalRef] public Image priceIcon;   // монета или значок видео слева от цены
        public GameObject ownedMark;
        public GameObject bestBadge;
        [OptionalRef] public TextMeshProUGUI bestText;

        public bool IsAd => productId != null && productId.StartsWith("ad_");
        public bool IsCoins => productId != null && productId.StartsWith("buy_");

        public void Refresh(GameApp app)
        {
            gameObject.SetActive(true);
            if (IsAd || IsCoins) { RefreshOwn(app); return; }
            var p = MetaCatalog.Product(productId);
            if (p == null) { gameObject.SetActive(false); return; }
            SetIcon(string.IsNullOrEmpty(iconName) ? p.Icon : iconName);
            title.text = p.Title;
            if (description != null && description.gameObject.activeSelf) description.text = p.Description;
            bool owned = p.Type == ProductType.Permanent &&
                         ((p.NoAds && p.Id == "no_ads" && app.Save.noAds) ||
                          (p.Id == "starter_pack" && app.Save.starterBought) ||
                          (p.Id != "starter_pack" && app.OwnsAll(p)));
            Owned = owned;
            // купленный набор оформления: кнопка ведёт в «Гардероб», где скины надеваются по отдельности
            bool set = p.Cosmetics != null && p.Id != "starter_pack";
            priceText.text = owned ? (set ? "Гардероб" : "Куплено") : Platform.PriceOf(p.Id);
            SetPriceIcon(null);
            buyButton.interactable = (!owned || set) && Platform.PaymentsAvailable;
            ownedMark.SetActive(owned && !set);
            buyButton.gameObject.SetActive(true);
            SetBadge(owned ? "" : badge);
        }

        void RefreshOwn(GameApp app)
        {
            Owned = false;
            ownedMark.SetActive(false);
            buyButton.gameObject.SetActive(true);
            bool can = true;
            string price;
            switch (productId)
            {
                case "ad_coins":
                    title.text = "Монеты за видео";
                    can = app.CanWatchCoinsAd;
                    price = "+" + app.AdCoinsAmount;
                    SetIcon(string.IsNullOrEmpty(iconName) ? "shop_free_coins" : iconName);
                    break;
                case "ad_hint":
                    title.text = "Подсказка за видео";
                    can = app.CanWatchHelpAd(true);
                    price = "+1";
                    SetIcon("shop_free_hint");
                    break;
                case "ad_undo":
                    title.text = "Отмена за видео";
                    can = app.CanWatchHelpAd(false);
                    price = "+1";
                    SetIcon("shop_free_undo");
                    break;
                case "ad_pack":
                    title.text = "Пачка за видео";
                    can = app.CanWatchPackAd;
                    price = "+1";
                    SetIcon("shop_free_pack");
                    break;
                case "buy_undo3":
                    title.text = "3 отмены";
                    price = Core.Economy.UndoPackPrice.ToString();
                    SetIcon("shop_undo3");
                    break;
                default:
                    title.text = "3 подсказки";
                    price = Core.Economy.HintPackPrice.ToString();
                    SetIcon("shop_hint3");
                    break;
            }
            if (description != null && description.gameObject.activeSelf) description.text = "";
            // лимит на сегодня исчерпан — кнопка гаснет, завтра снова
            priceText.text = can ? price : "Завтра";
            SetPriceIcon(!can ? null : IsAd ? "icon_video" : "icon_coin");
            buyButton.interactable = can;
            SetBadge(IsAd && can ? "" : badge);
        }

        void SetIcon(string sprite)
        {
            var sp = ArtLibrary.S(sprite);
            if (sp != null) icon.sprite = sp;
            icon.preserveAspect = true;
        }

        void SetPriceIcon(string sprite)
        {
            if (priceIcon == null) return;
            var sp = string.IsNullOrEmpty(sprite) ? null : ArtLibrary.S(sprite);
            priceIcon.gameObject.SetActive(sp != null);
            if (sp != null) { priceIcon.sprite = sp; priceIcon.preserveAspect = true; }
        }

        void SetBadge(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            bestBadge.SetActive(on);
            if (on && bestText != null) bestText.text = text;
        }
    }
}

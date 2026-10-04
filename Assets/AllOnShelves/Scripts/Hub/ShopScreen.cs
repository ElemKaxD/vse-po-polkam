using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Магазин по концепту пользователя (01.10.2026): пять вкладок — «Монеты», «Помощники», «Оформление»,
    /// «Наборы», «Без рекламы». Каждая вкладка — своя картинка-страница (Art/Shop/shop_page_N) с нарисованными
    /// вкладками, подиумом и пустыми карточками; игра кладёт сверху картинки товаров, названия и цены.
    /// Промты страниц и иконок — Docs/PROMPTS_Магазин_ВсёПоПолкам_30-09.txt.
    /// </summary>
    public class ShopScreen : HubScreen
    {
        public const int TabCount = 5;
        public static readonly string[] TabNames = { "Монеты", "Помощники", "Оформление", "Наборы", "Без рекламы" };

        public GameObject[] pages = new GameObject[TabCount];
        public Button[] tabs = new Button[TabCount];
        public ShopCard[] cards = new ShopCard[0];      // все карточки всех страниц (и подиумы)
        [OptionalRef] public ShopCard starterHero;      // подиум «Наборов»: стартовый, после покупки — «Набор строителя»
        [OptionalRef] public ShopCard starterSwap;      // первая карточка «Наборов»: строитель, после — большой помощник
        public GameObject unavailable;
        public Button[] themeApplyButtons = new Button[0]; // устарело: тему включает сама карточка

        int _tab;

        void Awake()
        {
            foreach (var c in cards)
            {
                var card = c;
                card.buyButton.onClick.AddListener(() => OnCard(card));
            }
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => { if (_tab != t) ShowTab(t); });
            }
        }

        /// <summary>Открыть магазин на нужной вкладке (0 — «Монеты»).</summary>
        public void OpenTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabCount - 1);
            if (IsOpen) Refresh();
            else hub.Show(this);
        }

        public void ShowTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabCount - 1);
            Refresh();
        }

        public override void Close()
        {
            base.Close();
            _tab = 0;   // в следующий раз — снова с монет
        }

        public override void Refresh()
        {
            var app = GameApp.I;
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == _tab);
            unavailable.SetActive(!Platform.PaymentsAvailable);
            bool starter = app.Save.starterBought;
            if (starterHero != null)
            {
                starterHero.productId = starter ? "builder_pack" : "starter_pack";
                starterHero.iconName = starter ? "shop_pack_builder" : "shop_hero_starter";
                starterHero.badge = starter ? "Новое" : "Выгодно";
            }
            if (starterSwap != null)
            {
                starterSwap.productId = starter ? "helpers_big" : "builder_pack";
                starterSwap.iconName = starter ? "shop_helpers_big" : "shop_pack_builder";
            }
            foreach (var c in cards) c.Refresh(app);
        }

        void OnCard(ShopCard card)
        {
            var app = GameApp.I;
            string id = card.productId;
            switch (id)
            {
                case "ad_coins":
                    if (!app.CanWatchCoinsAd) return;
                    Platform.ShowRewarded("reward_coins_shop", () =>
                    {
                        int n = app.AdCoinsAmount;
                        app.ClaimAdCoins();
                        hub.FlyCoinsToTop(card.transform.position, 5);
                        Toast.Show($"+{n} монет", "icon_coin");
                        Done();
                    });
                    return;
                case "ad_hint":
                case "ad_undo":
                    bool hint = id == "ad_hint";
                    if (!app.CanWatchHelpAd(hint)) return;
                    Platform.ShowRewarded(hint ? "reward_hint_shop" : "reward_undo_shop", () =>
                    {
                        app.ClaimHelpAd(hint);
                        Toast.Show(hint ? "+1 подсказка" : "+1 отмена", hint ? "icon_hint" : "icon_undo");
                        Done();
                    });
                    return;
                case "ad_pack":
                    if (!app.CanWatchPackAd) return;
                    Platform.ShowRewarded("reward_pack_shop", () =>
                    {
                        app.Save.packsOpenedToday++;
                        app.Save.pendingPacks++;
                        app.MarkDirty();
                        hub.packPopup.OpenPending(hub, Refresh);
                    });
                    return;
                case "buy_undo3":
                case "buy_hint3":
                    bool h = id == "buy_hint3";
                    int price = h ? Core.Economy.HintPackPrice : Core.Economy.UndoPackPrice;
                    if (!app.TrySpend(price)) { hub.CoinShortage(price - app.Coins, Refresh); return; }
                    if (h) { app.Save.hintGranted = true; app.AddHint(3); } else app.AddUndo(3);
                    AudioService.Play("sfx_purchase_success");
                    Toast.Show(h ? "+3 подсказки" : "+3 отмены", h ? "icon_hint" : "icon_undo");
                    Done();
                    return;
            }
            var p = MetaCatalog.Product(id);
            if (p != null && p.Cosmetics != null && card.Owned)
            {
                hub.wardrobePopup.Open(1);
                return;
            }
            Buy(card, id);
        }

        void Done()
        {
            hub.RefreshTop();
            Refresh();
        }

        void Buy(ShopCard card, string id)
        {
            Platform.Buy(id, _ =>
            {
                AudioService.Play("sfx_purchase_success"); Vfx.Play("vfx_gift_pop", card.transform.position, 700f);
                var p = MetaCatalog.Product(id);
                hub.confirmPopup.Info("Спасибо за покупку!", p != null ? p.Description : "", p != null ? p.Icon : "icon_gift",
                                      () => { if (GameApp.I.Save.pendingPacks > 0 && p != null && p.Packs > 0) hub.packPopup.OpenPending(hub, Refresh); });
                if (p != null && p.Coins > 0) hub.FlyCoinsToTop(card.transform.position, 8);
                hub.RefreshTop();
                Refresh();
            });
        }
    }
}

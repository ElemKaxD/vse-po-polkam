using System;
using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Окно «Алмазы» (экономика v4, 03.10.2026): две вкладки — «Алмазы» (6 наборов за деньги) и «Для дома»
    /// (ускорители стройки и молоток за алмазы). Открывается со счётчика алмазов и из стройки комнаты.
    /// Когда придут страницы магазина shop_page_6_gems / shop_page_7_house, эти вкладки переедут в книгу магазина.
    /// </summary>
    public class GemShopPopup : Popup
    {
        public Button[] tabs = new Button[2];
        public Image[] tabBgs = new Image[2];
        public GemShopCard[] cards = new GemShopCard[6];
        public TextMeshProUGUI gemsText;
        public Button closeButton;

        public static readonly string[] GemProducts = { "gems_30", "gems_90", "gems_180", "gems_400", "gems_850", "gems_1800" };

        HubController _hub;
        int _tab;
        Action _after;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                tabs[i].onClick.AddListener(() => { AudioService.Play("sfx_button"); SetTab(k); });
            }
            for (int i = 0; i < cards.Length; i++)
            {
                int k = i;
                cards[i].buy.onClick.AddListener(() => Buy(k));
            }
            closeButton.onClick.AddListener(() => Hide(() => { var a = _after; _after = null; a?.Invoke(); }));
        }

        void OnEnable() { GameApp.WalletChanged += Refresh; }
        void OnDisable() { GameApp.WalletChanged -= Refresh; }

        /// <param name="tab">0 — «Алмазы», 1 — «Для дома»</param>
        /// <param name="after">что сделать, когда окно закроют (вернуться к стройке)</param>
        public void Open(HubController hub, int tab = 0, Action after = null)
        {
            _hub = hub;
            _after = after;
            Show();
            SetTab(tab);
        }

        void SetTab(int t)
        {
            _tab = t;
            for (int i = 0; i < tabBgs.Length; i++)
                if (tabBgs[i] != null) tabBgs[i].color = i == t ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            Refresh();
        }

        void Refresh()
        {
            if (GameApp.I == null || !gameObject.activeSelf) return;
            var app = GameApp.I;
            gemsText.text = app.Gems.ToString();
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (_tab == 0)
                {
                    var p = MetaCatalog.Product(GemProducts[i]);
                    c.icon.sprite = ArtLibrary.S(p.Icon) ?? ArtLibrary.S("gem_pack_5");
                    c.title.text = p.Gems.ToString();
                    c.titleIcon.gameObject.SetActive(true);
                    c.owned.gameObject.SetActive(false);
                    c.priceIcon.gameObject.SetActive(false);
                    c.price.rectTransform.anchoredPosition = new Vector2(0f, 3f);
                    c.price.text = Platform.PriceOf(p.Id);
                    int bonus = BonusPct(p);
                    c.badge.SetActive(bonus > 0);
                    if (bonus > 0) c.badgeText.text = $"+{bonus} %";
                }
                else
                {
                    bool hammer = i == Economy.BoostMinutes.Length;
                    c.icon.sprite = ArtLibrary.S(hammer ? "icon_hammer_gold" : Economy.BoostIcons[i]);
                    c.title.text = hammer ? "Готово сразу" : "−" + Economy.Dur(Economy.BoostMinutes[i]);
                    c.titleIcon.gameObject.SetActive(false);
                    int have = hammer ? app.Save.hammers : app.Boosts(i);
                    c.owned.gameObject.SetActive(have > 0);
                    c.owned.text = "×" + have;
                    c.priceIcon.gameObject.SetActive(true);
                    c.price.rectTransform.anchoredPosition = new Vector2(16f, 3f);
                    c.price.text = (hammer ? Economy.GemHammerPrice : Economy.BoostPrice[i]).ToString();
                    c.badge.SetActive(false);
                }
                c.icon.preserveAspect = true;
            }
        }

        /// <summary>Выгода набора против самого маленького (по цене-заглушке, как в консоли).</summary>
        static int BonusPct(ProductInfo p)
        {
            var baseP = MetaCatalog.Product(GemProducts[0]);
            float rub = Rub(p.FallbackPrice), rub0 = Rub(baseP.FallbackPrice);
            if (rub <= 0 || rub0 <= 0) return 0;
            float k = p.Gems / rub / (baseP.Gems / rub0);
            return Mathf.RoundToInt((k - 1f) * 100f);
        }

        static float Rub(string price)
        {
            if (string.IsNullOrEmpty(price)) return 0;
            var digits = new System.Text.StringBuilder();
            foreach (char ch in price) if (char.IsDigit(ch)) digits.Append(ch);
            return float.TryParse(digits.ToString(), out var v) ? v : 0;
        }

        void Buy(int i)
        {
            var app = GameApp.I;
            if (_tab == 0)
            {
                string id = GemProducts[i];
                Platform.Buy(id, _ =>
                {
                    Vfx.At("vfx_gem_burst", cards[i].icon.transform, 380f);
                    if (_hub != null) _hub.FlyGemsToTop(cards[i].icon.transform.position, 6);
                    Toast.Show($"+{MetaCatalog.Product(id).Gems} алмазов", "icon_gem");
                    Refresh();
                });
                return;
            }
            bool hammer = i == Economy.BoostMinutes.Length;
            int price = hammer ? Economy.GemHammerPrice : Economy.BoostPrice[i];
            if (!app.TrySpendGems(price))
            {
                AudioService.Play("sfx_nope");
                Toast.Show("Не хватает алмазов", "icon_gem");
                SetTab(0);
                return;
            }
            if (hammer) app.AddHammers(1); else app.AddBoost(i, 1);
            Vfx.At("vfx_tile_bought", cards[i].icon.transform, 260f);
            Tween.Punch(cards[i].icon.transform, 0.2f, 0.3f);
        }
    }
}

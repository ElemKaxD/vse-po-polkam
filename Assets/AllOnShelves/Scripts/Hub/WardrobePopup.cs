using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// «Гардероб»: две вкладки. «Енот» — ореол вокруг енота на карте, скин енота, вид товаров;
    /// «Магазин» — оформление магазина, тележка, конвейер, стеллажи в уровне (30.09.2026).
    /// Нажатие на свою вещь надевает или снимает её. Чужая показана бледной, под ней — откуда её взять:
    /// цена набора за деньги (нажатие открывает предложение набора), звёзды «Звёздного пути» или район,
    /// за ремонт которого её дают. За монеты скины не продаются (решение 30.09.2026).
    /// </summary>
    public class WardrobePopup : Popup
    {
        public TextMeshProUGUI info;
        public Button closeButton;
        public Button[] tabs = new Button[3];
        public GameObject[] tabOn = new GameObject[3];      // подсветка выбранной вкладки
        public TextMeshProUGUI[] rowTitles = new TextMeshProUGUI[4];
        public Button[] slots = new Button[24];      // 4 ряда по 6
        public Image[] slotIcons = new Image[24];
        public GameObject[] slotWorn = new GameObject[24];
        public GameObject[] slotPrice = new GameObject[24];
        public TextMeshProUGUI[] slotPriceText = new TextMeshProUGUI[24];

        public const int PerRow = 6;
        public const int Rows = 4;

        // третья вкладка (01.10.2026) — аватарка и рамка в рейтинге: зверьков 12 — два ряда одного вида
        static readonly string[][] PageKinds = { new[] { "halo", "raccoon", "items" }, new[] { "scene", "cart", "belt", "shelf" },
                                                 // рамок с альбомом (v4) стало 16 — два ряда: свои первыми
                                                 new[] { "avatar", "avatar", "frame", "frame" } };
        static readonly Dictionary<string, string> RowNames = new Dictionary<string, string>
        {
            { "halo", "Ореол енота" }, { "raccoon", "Скин енота" }, { "items", "Вид товаров" },
            { "scene", "Оформление магазина" }, { "cart", "Тележка" }, { "belt", "Конвейер" }, { "shelf", "Стеллажи" },
            { "avatar", "Аватарка в рейтинге" }, { "frame", "Рамка аватарки" },
        };

        readonly List<string> _ids = new List<string>();
        int _page;
        HubController _hub;

        void OnEnable() { GameApp.WalletChanged += OnWallet; }
        void OnDisable() { GameApp.WalletChanged -= OnWallet; }

        // набор купили из окна предложения поверх «Гардероба» — сразу показываем новые вещи
        void OnWallet() { if (IsOpen) Refresh(); }

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
            for (int i = 0; i < slots.Length; i++)
            {
                int idx = i;
                slots[i].onClick.AddListener(() => Toggle(idx));
            }
            for (int t = 0; t < tabs.Length; t++)
            {
                int page = t;
                if (tabs[t] != null) tabs[t].onClick.AddListener(() => { _page = page; AudioService.Play("sfx_button"); Refresh(); });
            }
        }

        public void Open() => Open(0);

        public void Open(int page)
        {
            _page = Mathf.Clamp(page, 0, PageKinds.Length - 1);
            Show();
            Refresh();
        }

        void Toggle(int slot)
        {
            if (slot >= _ids.Count || string.IsNullOrEmpty(_ids[slot])) return;
            var app = GameApp.I;
            string id = _ids[slot];
            if (!app.HasCosmetic(id))
            {
                var c = MetaCatalog.Cosmetic(id);
                switch (MetaCatalog.SourceOf(id, out int number, out var product))
                {
                    case MetaCatalog.SourceKind.Product:
                        // набор за деньги: окно предложения поверх «Гардероба»
                        if (_hub == null) _hub = FindFirstObjectByType<HubController>();
                        AudioService.Play("sfx_button");
                        if (_hub != null && Platform.PaymentsAvailable) _hub.offerPopup.Open(product, 0);
                        else Toast.Show($"{c.Title} — в наборе «{product.Title}»", "icon_shop");
                        break;
                    case MetaCatalog.SourceKind.GoldPath:
                        AudioService.Play("sfx_nope");
                        Toast.Show($"{c.Title} — награда Золотого пути за {number} звёзд", "gold_path_card");
                        break;
                    case MetaCatalog.SourceKind.StarTrack:
                        AudioService.Play("sfx_nope");
                        Toast.Show($"{c.Title} — награда «Звёздного пути» за {number} звёзд", "icon_star");
                        break;
                    case MetaCatalog.SourceKind.Reno:
                        AudioService.Play("sfx_nope");
                        Toast.Show($"{c.Title} — награда за ремонт района {number}", "icon_hammer");
                        break;
                    case MetaCatalog.SourceKind.Album:
                        AudioService.Play("sfx_nope");
                        Toast.Show(number >= 0 ? $"{c.Title} — собери в альбоме отдел «{MetaCatalog.DepartmentNames[number]}»"
                                               : $"{c.Title} — собери весь альбом", "album_pack_closed");
                        break;
                    case MetaCatalog.SourceKind.Leaderboard:
                        AudioService.Play("sfx_nope");
                        Toast.Show($"{c.Title} — за {(number >= 4 ? "4–10" : number.ToString())} место недели в рейтинге", "icon_trophy");
                        break;
                }
                return;
            }
            app.Wear(id);
            AudioService.Play("sfx_button");
            Vfx.At("vfx_skin_equip", slots[slot].transform, 240f);
            Refresh();
        }

        void Refresh()
        {
            var app = GameApp.I;
            _ids.Clear();
            for (int i = 0; i < slots.Length; i++) _ids.Add("");
            for (int t = 0; t < tabOn.Length; t++) if (tabOn[t] != null) tabOn[t].SetActive(t == _page);

            // ряды без единой вещи (вид товаров, пока нарисованы не все товары) не показываем
            var kinds = PageKinds[_page].Where(k => MetaCatalog.CosmeticsOf(k).Any(c => c.ArtReady)).ToList();
            var used = new Dictionary<string, int>();   // вид в двух рядах подряд: второй ряд продолжает первый
            for (int r = 0; r < Rows; r++)
            {
                string kind = r < kinds.Count ? kinds[r] : null;
                int skip = kind != null && used.TryGetValue(kind, out int u) ? u : 0;
                if (kind != null) used[kind] = skip + PerRow;
                rowTitles[r].gameObject.SetActive(kind != null && skip == 0);
                if (kind != null) rowTitles[r].text = RowNames[kind];
                // своё — первым, затем то, что можно получить; без картинки вещь не показываем
                var list = kind == null ? new List<Cosmetic>()
                    : MetaCatalog.CosmeticsOf(kind).Where(c => c.ArtReady && (app.HasCosmetic(c.Id) || MetaCatalog.SourceOf(c.Id, out _, out _) != MetaCatalog.SourceKind.None))
                                 .OrderBy(c => app.HasCosmetic(c.Id) ? 0 : 1).Skip(skip).Take(PerRow).ToList();
                for (int k = 0; k < PerRow; k++)
                {
                    int i = r * PerRow + k;
                    bool has = k < list.Count;
                    slots[i].gameObject.SetActive(has);
                    if (!has) continue;
                    _ids[i] = list[k].Id;
                    slotIcons[i].sprite = ArtLibrary.S(list[k].IconSprite) ?? ArtLibrary.S(list[k].Sprite);
                    slotIcons[i].preserveAspect = true;
                    // ореол и рамка крупнее: они обрамляют енота или аватарку; остальное вписываем в кружок целиком
                    float box = kind == "halo" || kind == "frame" ? 92f : 76f;
                    slotIcons[i].rectTransform.sizeDelta = new Vector2(box, box);
                    bool mine = app.HasCosmetic(list[k].Id);
                    slotIcons[i].color = mine ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                    slotWorn[i].SetActive(mine && app.Worn(kind) == list[k].Id);
                    // под ореолом показываем самого енота: иначе кольцо ни о чём не говорит
                    var baseImg = slots[i].transform.Find("Base");
                    if (baseImg != null)
                    {
                        baseImg.gameObject.SetActive(kind == "halo" || kind == "frame");
                        // под рамкой — своя аватарка игрока, под ореолом — енот
                        var bi = baseImg.GetComponent<Image>();
                        bi.sprite = ArtLibrary.S(kind == "frame" ? League.MyAvatarSprite : "map_avatar");
                        bi.preserveAspect = true;
                        ((RectTransform)baseImg).sizeDelta = kind == "frame" ? new Vector2(64, 64) : new Vector2(50, 50);
                    }
                    if (i < slotPrice.Length && slotPrice[i] != null)
                    {
                        slotPrice[i].SetActive(!mine);
                        if (!mine) SetSource(i, list[k].Id);
                    }
                }
            }
            // подсказку убрали (30.09.2026): внизу окна её закрывало сердечко рамки, а ценники и так говорят сами
            info.gameObject.SetActive(false);
        }

        /// <summary>Ярлычок под закрытой вещью: цена набора, звёзды пути или номер района ремонта.</summary>
        void SetSource(int i, string id)
        {
            var kind = MetaCatalog.SourceOf(id, out int number, out var product);
            var icon = slotPrice[i].transform.Find("Coin")?.GetComponent<Image>();
            string sprite = "icon_coin", text = "";
            switch (kind)
            {
                case MetaCatalog.SourceKind.Product: sprite = "icon_shop"; text = Platform.PriceOf(product.Id); break;
                case MetaCatalog.SourceKind.StarTrack: sprite = "icon_star"; text = number.ToString(); break;
                case MetaCatalog.SourceKind.GoldPath: sprite = "gold_path_card"; text = number.ToString(); break;
                case MetaCatalog.SourceKind.Reno: sprite = "icon_hammer"; text = $"Район {number}"; break;
                case MetaCatalog.SourceKind.Leaderboard: sprite = "icon_trophy"; text = number >= 4 ? "4–10" : $"{number} место"; break;
                case MetaCatalog.SourceKind.Album: sprite = "album_pack_closed"; text = "Альбом"; break;
            }
            if (icon != null)
            {
                icon.sprite = ArtLibrary.S(sprite);
                icon.preserveAspect = true;
            }
            slotPriceText[i].text = text;
        }
    }
}

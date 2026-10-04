using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Секция стеллажа: 3 места (2 для крупного), ярлык, замок, акция, морозилка.</summary>
    public class SectionView : MonoBehaviour
    {
        public RectTransform rect;
        public Image frame;
        public Image tagBase;
        public Image tagIcon;
        public RectTransform[] slots = new RectTransform[3];
        public GameObject lockRoot;
        public TextMeshProUGUI lockText;
        public GameObject saleTag;
        public Image freezerGlass;
        public Image bigFrame;                 // шкаф под крупный товар, стоит низом на том же полу
        public RectTransform[] bigSlots = new RectTransform[2];
        public Image freezerFrame;             // морозилка вместо деревянного стеллажа (просьба 26.09.2026)
        public RectTransform[] freezerSlots = new RectTransform[3];
        public Image doorLamp;
        public Image glow;
        [Header("Полка за рекламу")]
        public GameObject adRoot;            // затемнённый шкаф с киноплёнкой: «посмотри рекламу — будет твой»
        public Button adButton;
        public Sprite normalSprite, bigSprite, freezerClosed, freezerOpen;

        /// <summary>Скин стеллажа (30.09.2026): те же пропорции и высоты полок, что у brd_section 1. null — обычный.</summary>
        public void SetSkin(Sprite skin)
        {
            if (frame != null) frame.sprite = skin != null ? skin : normalSprite;
        }

        /// <summary>Картинка шкафа и её пропорции — размеры секции считаем от неё, не растягивая.</summary>
        public const string Sprite = "brd_section 1";
        public const float Aspect = 800f / 1968f;
        /// <summary>Самый крупный шкаф: шире пользователь его не ставил.</summary>
        public const float MaxWidth = 221f;
        /// <summary>Соседние шкафы стоят вплотную, боковины чуть перекрываются.</summary>
        public const float Overlap = 1.21f;
        /// <summary>Доли высоты шкафа, где стоят полки: товар стоит на доске, а не висит (сняты с картинки;
        /// возвращены 22.09.2026 — «доска + половина слота» поднимала товар в воздух).</summary>
        public static readonly float[] SlotY = { 0.220f, -0.012f, -0.222f };
        /// <summary>Сторона места как доля высоты шкафа: почти вся ниша (просьба 21.09.2026: крупнее).</summary>
        public const float SlotFrac = 0.185f;

        /// <summary>Шкаф под крупный товар: своя картинка, две широкие ниши (замеры с brd_section_big).</summary>
        public const string BigSprite = "brd_section_big";
        public const float BigAspect = 396f / 640f;
        public static readonly float[] BigSlotY = { 0.211f, -0.191f };
        public const float BigSlotFrac = 0.29f;

        /// <summary>
        /// Морозилка — это холодильник, а не стеллаж со снежинкой (просьба 26.09.2026).
        /// Стоит низом на том же полу, что и стеллажи, и ниже их: у холодильника свои пропорции.
        /// Полки сняты с картинки brd_freezer_closed (доли высоты от центра, вверх — плюс).
        /// </summary>
        public const string FreezerSprite = "brd_freezer_closed";
        public const float FreezerAspect = 344f / 640f;
        public static readonly float[] FreezerSlotY = { 0.215f, 0.060f, -0.098f };
        public const float FreezerSlotFrac = 0.135f;

        /// <summary>Морозилку рисуем холодильником, только если картинка есть.</summary>
        public bool IsFreezerBox => Kind == SectionKind.Freezer && freezerFrame != null && freezerFrame.sprite != null;

        [HideInInspector] public SectionKind Kind;

        /// <summary>Показать шкаф как предложение «плюс полка за рекламу» (тёмный, с киноплёнкой).</summary>
        public void ShowAdOffer(bool on)
        {
            if (adRoot != null) adRoot.SetActive(on);
            frame.color = on ? new Color(0.42f, 0.38f, 0.36f, 1f) : Color.white;
            if (bigFrame != null) bigFrame.gameObject.SetActive(false);
            if (freezerFrame != null && on) freezerFrame.gameObject.SetActive(false);
            if (tagBase != null) tagBase.gameObject.SetActive(!on);
            if (lockRoot != null && on) lockRoot.SetActive(false);
            if (saleTag != null && on) saleTag.SetActive(false);
            if (freezerGlass != null && on) freezerGlass.gameObject.SetActive(false);
            foreach (var s in slots) if (s != null) s.gameObject.SetActive(!on);
        }
        float _saleY;
        int _homeIndex = -1;

        /// <summary>Расставляет места по нишам шкафа под текущий размер секции.</summary>
        public void LayoutSlots()
        {
            float w = rect.sizeDelta.x, h = rect.sizeDelta.y;
            float side = h * SlotFrac;
            for (int i = 0; i < slots.Length && i < SlotY.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].sizeDelta = new Vector2(side, side);
                slots[i].anchoredPosition = new Vector2(0f, SlotY[i] * h);
            }
            if (freezerFrame != null && freezerFrame.sprite != null)
            {
                // холодильник той же ширины: низ совпадает с низом стеллажа, пропорции родные
                float fh = w / FreezerAspect;
                var frt = freezerFrame.rectTransform;
                frt.sizeDelta = new Vector2(w, fh);
                frt.anchoredPosition = new Vector2(0f, (fh - h) / 2f);
                float fside = fh * FreezerSlotFrac;
                // лампочка дверцы — на синей крыше холодильника (раньше висела над ним в воздухе)
                if (doorLamp != null)
                    doorLamp.rectTransform.anchoredPosition = new Vector2(w * 0.30f, (fh - h) / 2f + fh * 0.405f);
                for (int i = 0; i < freezerSlots.Length && i < FreezerSlotY.Length; i++)
                {
                    if (freezerSlots[i] == null) continue;
                    freezerSlots[i].sizeDelta = new Vector2(fside, fside);
                    freezerSlots[i].anchoredPosition = new Vector2(0f, FreezerSlotY[i] * fh);
                }
            }
            if (bigFrame == null) return;
            // широкий шкаф той же ширины: низ совпадает с низом высокого, картинка в родных пропорциях
            float bh = w / BigAspect;
            var brt = bigFrame.rectTransform;
            brt.sizeDelta = new Vector2(w, bh);
            brt.anchoredPosition = new Vector2(0f, (bh - h) / 2f);
            float bside = bh * BigSlotFrac;
            for (int i = 0; i < bigSlots.Length && i < BigSlotY.Length; i++)
            {
                if (bigSlots[i] == null) continue;
                bigSlots[i].sizeDelta = new Vector2(bside, bside);
                bigSlots[i].anchoredPosition = new Vector2(0f, BigSlotY[i] * bh);
            }
        }

        /// <summary>Во сколько раз товар на полке меньше товара на ленте (112 точек).</summary>
        public float ItemScale => IsFreezerBox
            ? Mathf.Clamp(rect.sizeDelta.x / FreezerAspect * FreezerSlotFrac * 1.15f / 112f, 0.34f, 1f)
            : Mathf.Clamp(rect.sizeDelta.y * SlotFrac * 1.15f / 112f, 0.4f, 1f);

        /// <summary>Крупный товар стоит в широкой нише — он больше обычного.</summary>
        public float BigItemScale =>
            Mathf.Clamp(rect.sizeDelta.x / BigAspect * BigSlotFrac * 1.1f / 112f, 0.4f, 1.3f);

        public void Configure(SectionKind kind)
        {
            Kind = kind;
            saleTag.SetActive(kind == SectionKind.Sale);
            _saleY = ((RectTransform)saleTag.transform).anchoredPosition.y;
            // морозилка — отдельный шкаф-холодильник; деревянный стеллаж под ним не нужен
            if (freezerFrame != null) freezerFrame.gameObject.SetActive(IsFreezerBox);
            foreach (var s in freezerSlots) if (s != null) s.gameObject.SetActive(IsFreezerBox);
            frame.enabled = !IsFreezerBox;
            freezerGlass.gameObject.SetActive(kind == SectionKind.Freezer);
            doorLamp.gameObject.SetActive(false);
            SetHighlight(0);
        }

        public void Refresh(SectionState s, bool doorOpen, bool hasDoor)
        {
            bool big = s.Type >= 0 && ItemCatalog.IsBig(s.Type);
            frame.enabled = !big && !IsFreezerBox;
            if (bigFrame != null) bigFrame.gameObject.SetActive(big);
            if (freezerFrame != null) freezerFrame.gameObject.SetActive(IsFreezerBox && !big);
            bool hasTag = s.Type >= 0;
            tagBase.gameObject.SetActive(hasTag);
            if (hasTag)
            {
                tagBase.color = Color.Lerp(Color.white, ArtLibrary.DepartmentColor(ItemCatalog.Get(s.Type).Department), 0.35f);
                tagIcon.sprite = ArtLibrary.Item(s.Type);
                ArtLibrary.Fit(tagIcon, rect.sizeDelta.x * 0.28f);
                // у широкого шкафа нет арки — ярлык вешаем на его верхний край
                float bh = rect.sizeDelta.x / BigAspect;
                float fh = rect.sizeDelta.x / FreezerAspect;
                tagBase.rectTransform.anchoredPosition = new Vector2(0f,
                    big ? (bh - rect.sizeDelta.y) / 2f + bh * 0.42f
                    : IsFreezerBox ? (fh - rect.sizeDelta.y) / 2f + fh * 0.46f
                    : rect.sizeDelta.y * 0.398f);
                // ценник акции — на том же шкафу, что и ярлык, иначе висит рядом в воздухе
                var srt = (RectTransform)saleTag.transform;
                srt.anchoredPosition = new Vector2(srt.anchoredPosition.x,
                    big ? (bh - rect.sizeDelta.y) / 2f + bh * 0.26f : _saleY);
            }
            lockRoot.SetActive(s.Locked);
            if (s.Locked) lockText.text = s.LockSets.ToString();
            if (Kind == SectionKind.Freezer)
            {
                // «стекло» морозилки: закрыто — матовое голубое, открыто — почти прозрачное
                freezerGlass.color = doorOpen ? new Color(0.78f, 0.92f, 1f, 0.12f) : new Color(0.78f, 0.92f, 1f, 0.5f);
                doorLamp.gameObject.SetActive(hasDoor);
                doorLamp.color = doorOpen ? new Color(0.36f, 0.77f, 0.42f) : new Color(0.91f, 0.29f, 0.29f);
            }
        }

        public Vector3 SlotWorld(int index, int type)
        {
            if (ItemCatalog.IsBig(type))
            {
                // два места: верхняя и нижняя ниша широкого шкафа
                if (bigSlots != null && bigSlots[0] != null)
                    return bigSlots[Mathf.Clamp(index, 0, bigSlots.Length - 1)].position;
                Vector3 a = slots[0].position, c = slots[2].position;
                return index == 0 ? Vector3.Lerp(a, c, 0.18f) : Vector3.Lerp(a, c, 0.82f);
            }
            if (IsFreezerBox && freezerSlots != null && freezerSlots[0] != null)
                return freezerSlots[Mathf.Clamp(index, 0, freezerSlots.Length - 1)].position;
            return slots[Mathf.Clamp(index, 0, slots.Length - 1)].position;
        }

        /// <summary>0 — нет, 1 — можно положить, 2 — нельзя.</summary>
        public void SetHighlight(int state)
        {
            // боковины соседних шкафов заходят друг на друга: подсвеченный шкаф выносим вперёд,
            // иначе обводка уходит под соседа; без подсветки — возвращаем на своё место
            if (state != 0 && _homeIndex < 0) { _homeIndex = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
            else if (state == 0 && _homeIndex >= 0) { transform.SetSiblingIndex(_homeIndex); _homeIndex = -1; }
            glow.gameObject.SetActive(state != 0);
            glow.color = state == 1 ? new Color(0.36f, 0.85f, 0.45f, 0.9f) : new Color(0.95f, 0.3f, 0.3f, 0.8f);
        }

        public void PopLock()
        {
            Tween.Punch(lockRoot.transform, 0.3f, 0.35f);
        }

        public bool Contains(Vector2 screenPos, Camera cam) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }
}

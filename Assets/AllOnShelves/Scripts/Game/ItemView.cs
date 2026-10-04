using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Товар на поле (префаб ItemView). Ввод передаётся в GameController.</summary>
    public class ItemView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Графика")]
        public RectTransform rect;
        public RectTransform visual;
        public Image shadow;
        public Image icon;
        public Image icon2;
        public Image box;
        public Image palletBase;
        public Image tape;
        public Image glow;
        public Image frost;
        public Image fly;
        public Image timerBadge;
        public TextMeshProUGUI timerText;
        public CanvasGroup group;

        [HideInInspector] public int Uid;
        [HideInInspector] public int Type = -1;
        [HideInInspector] public int Type2 = -1;
        [HideInInspector] public TokenKind Kind;
        [HideInInspector] public bool Interactable;
        [HideInInspector] public bool Spoiled;
        [HideInInspector] public GameController Controller;

        bool _dragging;

        public void SetToken(Token t)
        {
            Kind = t.Kind;
            Type = t.Type;
            Type2 = t.Type2;
            bool hidden = t.Kind == TokenKind.Box && !t.Revealed;
            icon.enabled = !hidden;
            icon.sprite = ArtLibrary.Item(t.Type);
            box.gameObject.SetActive(hidden);
            palletBase.gameObject.SetActive(t.Kind == TokenKind.Pallet);
            tape.gameObject.SetActive(t.Kind == TokenKind.Bundle);
            icon2.gameObject.SetActive(t.Kind == TokenKind.Bundle || t.Kind == TokenKind.Pallet);
            if (t.Kind == TokenKind.Bundle)
            {
                icon2.sprite = ArtLibrary.Item(t.Type2);
                icon.rectTransform.anchoredPosition = new Vector2(-22, 6);
                icon2.rectTransform.anchoredPosition = new Vector2(24, -6);
                icon.rectTransform.localScale = icon2.rectTransform.localScale = Vector3.one * 0.72f;
            }
            else if (t.Kind == TokenKind.Pallet)
            {
                icon2.sprite = ArtLibrary.Item(t.Type);
                icon.rectTransform.anchoredPosition = new Vector2(-18, 14);
                icon2.rectTransform.anchoredPosition = new Vector2(20, 14);
                icon.rectTransform.localScale = icon2.rectTransform.localScale = Vector3.one * 0.66f;
            }
            else ResetLayout();
            SetTimer(-1);
            SetSpoiled(false);
            SetFrozenLook(ItemCatalog.IsFrozen(t.Type) && !hidden);
            ApplyBigScale();
            PlaceShadow();
        }

        public void SetItem(int type)
        {
            Kind = TokenKind.Item;
            Type = type;
            Type2 = -1;
            icon.enabled = true;
            icon.sprite = ArtLibrary.Item(type);
            box.gameObject.SetActive(false);
            palletBase.gameObject.SetActive(false);
            tape.gameObject.SetActive(false);
            icon2.gameObject.SetActive(false);
            ResetLayout();
            SetFrozenLook(ItemCatalog.IsFrozen(type));
            ApplyBigScale();
            PlaceShadow();
        }

        void ResetLayout()
        {
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Овальная тень под товаром (спрайт fx_shadow_oval): центр овала — на нижнем
        /// контуре продукта (просьба 22.09.2026): тень выглядывает мягким серпом снизу и с боков.</summary>
        public void PlaceShadow()
        {
            if (shadow == null) return;
            // рисованную ширину даёт иконка с её локальным масштабом (у связок/паллет он 0.72/0.66)
            float w = icon.rectTransform.rect.width * icon.rectTransform.localScale.x;
            float h = icon.rectTransform.rect.height * icon.rectTransform.localScale.y;
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -h / 2f);
            shadow.rectTransform.sizeDelta = new Vector2(w * 1.06f, w * 0.32f);
        }

        void ApplyBigScale()
        {
            visual.localScale = ItemCatalog.IsBig(Type) && Kind == TokenKind.Item ? Vector3.one * 1.25f : Vector3.one;
        }

        public void RevealBox(int type)
        {
            Tween.Scale(visual, new Vector3(0f, 1f, 1f), 0.12f, Ease.InQuad, () =>
            {
                SetItem(type);
                visual.localScale = new Vector3(0f, 1f, 1f);
                Tween.Scale(visual, ItemCatalog.IsBig(type) ? Vector3.one * 1.25f : Vector3.one, 0.18f, Ease.OutBack);
            });
        }

        public void SetTimer(int value)
        {
            bool on = value > 0 && !Spoiled;
            timerBadge.gameObject.SetActive(on);
            if (on) timerText.text = value.ToString();
        }

        public void SetSpoiled(bool spoiled)
        {
            Spoiled = spoiled;
            fly.gameObject.SetActive(spoiled);
            icon.color = spoiled ? new Color(0.62f, 0.5f, 0.38f, 1f) : Color.white;
            if (spoiled) timerBadge.gameObject.SetActive(false);
        }

        public void SetFrozenLook(bool on) => frost.gameObject.SetActive(on);

        public void SetHighlight(bool on)
        {
            glow.gameObject.SetActive(on);
        }

        public void SetInteractable(bool on)
        {
            Interactable = on;
            group.blocksRaycasts = true;
        }

        // ------------------------------------------------------------------ ввод

        public void OnPointerClick(PointerEventData e)
        {
            if (_dragging || e.dragging) return;
            Controller?.OnItemClicked(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (Controller == null || !Controller.CanDrag(this)) { e.pointerDrag = null; return; }
            _dragging = true;
            Controller.OnItemDragBegin(this, e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) Controller?.OnItemDrag(this, e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            Controller?.OnItemDragEnd(this, e);
        }

        public void OnPointerEnter(PointerEventData e) => Controller?.OnItemHover(this, true);
        public void OnPointerExit(PointerEventData e) => Controller?.OnItemHover(this, false);
    }
}

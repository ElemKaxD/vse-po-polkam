using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Тележка: всегда спрайт на 5 ячеек (brd_cart_5 или его тема), по размеру подходит под товары.
    /// Вместимость меньше 5 — лишние ячейки закрыты замком; больше 5 — ящики грузчика справа.
    /// Подсветка — тонировкой самой тележки, без прямоугольных рамок.
    /// </summary>
    public class CartView : MonoBehaviour
    {

        /// <summary>Пропорции тележки и доли ячеек — сняты с самой картинки brd_cart_5.</summary>
        public const float Aspect = 1020f / 464f;
        public const float SlotStep = 0.1672f;   // шаг ячеек как доля ширины
        public const float SlotY = 0.0550f;      // подъём ячеек как доля высоты
        public const float SlotFrac = 0.1480f;   // сторона ячейки как доля ширины

        /// <summary>Во сколько раз товар в тележке меньше товара на ленте (112 точек).</summary>
        public float ItemScale => Mathf.Clamp(rect.sizeDelta.x * SlotFrac * 1.05f / 112f, 0.4f, 1f);
        public RectTransform rect;
        public Image body;
        public RectTransform slotsRoot;
        public RectTransform[] slots = new RectTransform[6];
        public GameObject[] slotLocks = new GameObject[0];
        public Sprite cart5;

        static readonly Color Normal = Color.white;
        static readonly Color Good = new Color(0.72f, 1f, 0.72f, 1f);
        static readonly Color Bad = new Color(1f, 0.66f, 0.66f, 1f);

        int _capacity = 5;
        int _highlight;
        bool _warning;

        public GameObject[] extraCrates = new GameObject[0];

        public void Configure(int capacity, Sprite skin)
        {
            _capacity = Mathf.Min(capacity, slots.Length);
            body.sprite = skin != null ? skin : cart5;
            const float spacing = 116f;
            const float start = -2f * spacing;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].gameObject.SetActive(i < _capacity);
                // места сверх корзины — ящики грузчика справа от тележки
                slots[i].anchoredPosition = i < 5 ? new Vector2(start + i * spacing, 0f) : new Vector2(430f + (i - 5) * 120f, -60f);
            }
            for (int i = 0; i < slotLocks.Length; i++) slotLocks[i].SetActive(i >= _capacity);
            for (int i = 0; i < extraCrates.Length; i++) extraCrates[i].SetActive(i + 5 < _capacity);
            _warning = false;
            Tween.Kill(body);
            SetHighlight(0);
        }

        public Vector3 SlotWorld(int slotIndex, int size)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, _capacity - 1);
            if (size == 2 && slotIndex + 1 < _capacity) return Vector3.Lerp(slots[slotIndex].position, slots[slotIndex + 1].position, 0.5f);
            return slots[slotIndex].position;
        }

        public void SetWarning(bool on)
        {
            if (_warning == on) return;
            _warning = on;
            Tween.Kill(body);
            if (on)
            {
                Tween.Run(body, Tween.ChAlpha, 60f, Ease.Linear, k =>
                {
                    if (body == null || _highlight != 0) return;
                    body.color = Color.Lerp(Normal, Bad, 0.5f + 0.5f * Mathf.Sin(k * 60f * 5f));
                });
            }
            else ApplyColor();
        }

        public void SetHighlight(int state)
        {
            _highlight = state;
            ApplyColor();
        }

        void ApplyColor() => body.color = _highlight == 1 ? Good : _highlight == 2 ? Bad : Normal;

        public bool Contains(Vector2 screenPos, Camera cam) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }
}

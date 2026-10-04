using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Улучшение ремонта. Показывается двумя способами (референс игрока 26.09.2026):
    /// кружком-меткой прямо на магазине (мелкая иконка на светящейся ниточке — просто показывает,
    /// где встанет улучшение; купить с него нельзя) и плиткой в нижней панели
    /// (крупная иконка без подписи + ценник — покупают там). Поля, которых нет, — null.
    /// </summary>
    public class RenoCard : MonoBehaviour
    {
        [OptionalRef] public Image cardBg;        // плитка нижней панели (у метки — null)
        [OptionalRef] public Image bubble;        // кружок метки (у плитки — null)
        [OptionalRef] public Sprite bgNormal, bgBought;   // рамка плитки: обычная и золотая
        public Image icon;
        [OptionalRef] public TextMeshProUGUI title;
        [OptionalRef] public TextMeshProUGUI unlocks;
        [OptionalRef] public Button buyButton;    // у метки кнопки нет: покупают только с плитки
        [OptionalRef] public TextMeshProUGUI costText;
        public GameObject boughtMark;
        [OptionalRef] public RectTransform thread;
        [OptionalRef] public HoverHint hintBadge;   // «?» в углу плитки: наводишь — пишет, что откроет

        [HideInInspector] public RenovationItem item;
        [HideInInspector] public bool bought;

        public void Set(RenovationItem it, bool bought, bool affordable, bool open, string iconSprite = null)
        {
            item = it;
            this.bought = bought;
            // облачко купленного улучшения убираем совсем: предмет уже стоит на магазине.
            // плитка остаётся на месте — с галочкой, чтобы панель не «прыгала» после покупки
            if (thread != null)
            {
                // этап ещё закрыт — облачка нет совсем, иначе от него остаётся висеть ниточка
                gameObject.SetActive(!bought && open);
                if (bought || !open) return;
            }
            // сначала нарисованная иконка предмета, потом замена из другой серии, и только
            // потом слой, вырезанный из кадра «лего»
            icon.sprite = ArtLibrary.S(it.Id) ?? ArtLibrary.S(MetaCatalog.IconFor(it.Id)) ?? ArtLibrary.S(iconSprite);
            icon.preserveAspect = true;
            // иконка всегда в полную силу: и купленное, и непокупленное видно крупно и ярко
            // (просьба 26.09.2026). Что куплено — показывает золотая рамка и галочка
            icon.color = Color.white;
            if (title != null)
            {
                title.text = it.Name;
                title.gameObject.SetActive(!bought);
            }
            if (unlocks != null)
            {
                unlocks.text = string.IsNullOrEmpty(it.Unlocks) ? "" : "+ " + it.Unlocks;
                unlocks.gameObject.SetActive(!bought && !string.IsNullOrEmpty(it.Unlocks));
            }
            if (hintBadge != null) hintBadge.gameObject.SetActive(!bought && !string.IsNullOrEmpty(it.Unlocks));
            if (costText != null)
            {
                costText.text = it.Cost.ToString();
                costText.color = affordable ? Color.white : new Color(1f, 0.85f, 0.85f);
            }
            if (boughtMark != null) boughtMark.SetActive(bought);
            if (buyButton != null) buyButton.gameObject.SetActive(!bought && open);
            if (cardBg != null && bgNormal != null && bgBought != null)
                cardBg.sprite = bought ? bgBought : bgNormal;
            if (cardBg != null) cardBg.color = open ? Color.white : new Color(0.84f, 0.84f, 0.84f);
            if (bubble != null) bubble.color = Color.white;
        }

        /// <summary>Тянет ниточку от кружка к месту, где появится улучшение.</summary>
        public void PointTo(Vector2 localTarget)
        {
            if (thread == null) return;
            var from = ((RectTransform)transform).anchoredPosition;
            var d = localTarget - from;
            float len = d.magnitude;
            thread.sizeDelta = new Vector2(Mathf.Max(0f, len - 62f), thread.sizeDelta.y);
            thread.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Оффер (ГДД 11.2): стартовый набор, без рекламы, набор помощника.</summary>
    public class OfferPopup : Popup
    {
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI description;
        public TextMeshProUGUI timer;
        public Button buyButton;
        public TextMeshProUGUI priceText;
        public Button closeButton;

        long _expires;
        string _id;
        Vector2 _iconSize;

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
            buyButton.onClick.AddListener(() => Platform.Buy(_id, _ =>
            {
                AudioService.Play("sfx_purchase_success"); Vfx.Play("vfx_gift_pop", transform.position, 700f);
                var p = MetaCatalog.Product(_id);
                Toast.Show(p != null && p.Cosmetics != null ? "Спасибо! Скины уже надеты — сменить можно в «Гардеробе»" : "Спасибо за покупку!");
                Hide();
            }));
        }

        public void Open(ProductInfo p, long expires)
        {
            if (p == null) return;
            _id = p.Id;
            _expires = expires;
            icon.sprite = ArtLibrary.S(p.Icon);
            icon.preserveAspect = true;
            // широкая картинка (превью фона у наборов оформления) — коробка шире при той же высоте, иначе превью крошечное
            var rt = icon.rectTransform;
            if (_iconSize == Vector2.zero) _iconSize = rt.sizeDelta;
            float aspect = icon.sprite != null ? icon.sprite.rect.width / icon.sprite.rect.height : 1f;
            rt.sizeDelta = aspect > 1.3f ? new Vector2(Mathf.Min(420f, _iconSize.y * aspect), _iconSize.y) : _iconSize;
            title.text = p.Title;
            description.text = p.Description;
            priceText.text = Platform.PriceOf(p.Id);
            timer.gameObject.SetActive(expires > 0);
            Show();
        }

        void Update()
        {
            if (_expires <= 0 || !timer.gameObject.activeSelf) return;
            long left = _expires - GameApp.NowUnix;
            if (left < 0) left = 0;
            timer.text = $"Осталось {left / 3600}:{left % 3600 / 60:00}:{left % 60:00}";
        }
    }
}

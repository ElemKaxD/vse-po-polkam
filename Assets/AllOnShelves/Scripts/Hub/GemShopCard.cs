using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Карточка окна «Алмазы»: картинка, количество, кнопка цены, бирка выгоды, «×N у тебя».</summary>
    public class GemShopCard : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI title;
        public Image titleIcon;            // алмаз рядом с количеством (вкладка «Алмазы»)
        public TextMeshProUGUI owned;      // сколько ускорителей уже есть
        public Button buy;
        public TextMeshProUGUI price;
        public Image priceIcon;            // алмаз на кнопке (вкладка «Для дома»)
        public GameObject badge;           // «+29 %»
        public TextMeshProUGUI badgeText;
    }
}

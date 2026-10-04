using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Плитка декора на доске экрана комнаты: сам предмет, вырезанный из комнаты, и ценник.</summary>
    public class DecorTile : MonoBehaviour
    {
        public Image bg;
        public Image icon;
        public Button buyButton;
        public TextMeshProUGUI costText;
        public GameObject boughtMark;
        public Image lockMark;
        public Sprite bgNormal, bgBought;
        [HideInInspector] public int item;   // 1..6

        /// <summary>bought — уже стоит в комнате; next — следующий к покупке; иначе — ждёт своей очереди.</summary>
        public void Set(string iconSprite, bool bought, bool next, int cost, bool afford)
        {
            var sp = ArtLibrary.S(iconSprite);
            icon.sprite = sp;
            icon.preserveAspect = true;
            icon.color = bought || next ? Color.white : new Color(0.55f, 0.52f, 0.5f, 0.85f);
            bg.sprite = bought && bgBought != null ? bgBought : bgNormal;
            boughtMark.SetActive(bought);
            buyButton.gameObject.SetActive(!bought);
            buyButton.interactable = next;
            costText.text = cost.ToString();
            costText.color = !next || afford ? Color.white : new Color(1f, 0.75f, 0.7f);
            if (lockMark != null) lockMark.gameObject.SetActive(!bought && !next);
        }
    }
}

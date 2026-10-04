using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Место в альбоме: наклейка как наклейка, без рамки; пока не открыта — тёмный силуэт.</summary>
    public class StickerCell : MonoBehaviour
    {
        public Image frame;
        public Image image;
        public Image shine;
        public TextMeshProUGUI label;
        public GameObject question;

        public void Set(string id, bool owned, bool gold)
        {
            image.sprite = ArtLibrary.Sticker(id);
            image.preserveAspect = true;
            image.color = owned ? (gold ? new Color(1f, 0.85f, 0.35f) : Color.white) : new Color(0.25f, 0.2f, 0.18f, 0.35f);
            question.SetActive(!owned);
            if (label.gameObject.activeSelf) label.text = owned ? MetaCatalog.StickerName(id) : "???";
            shine.gameObject.SetActive(owned && gold);
        }
    }
}

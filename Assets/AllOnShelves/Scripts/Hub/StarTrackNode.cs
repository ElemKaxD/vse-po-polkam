using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Одна награда «Звёздного пути»: сколько звёзд нужно, что дают, «Забрать» / галочка.</summary>
    public class StarTrackNode : MonoBehaviour
    {
        public TextMeshProUGUI starsText;
        public Image card;
        public Image icon;
        public TextMeshProUGUI caption;
        public Button claimButton;
        public GameObject done;
        public GameObject lockIcon;
        // Золотой путь — вторая строка (v4, 03.10.2026)
        public Image goldCard;
        public Image goldIcon;
        public TextMeshProUGUI goldCaption;
        public Button goldClaim;
        public GameObject goldDone;
        public GameObject goldLock;

        public void SetGold(StarReward r, bool owned, bool reached, bool claimed)
        {
            goldIcon.sprite = ArtLibrary.S(r.Icon);
            goldIcon.preserveAspect = true;
            goldCaption.text = r.Caption;
            bool ready = owned && reached && !claimed;
            goldClaim.gameObject.SetActive(ready);
            goldDone.SetActive(claimed);
            goldLock.SetActive(!owned || !reached);
            var tint = owned && reached ? Color.white : new Color(0.72f, 0.68f, 0.64f, 1f);
            goldCard.color = tint;
            goldIcon.color = claimed ? new Color(1f, 1f, 1f, 0.55f) : tint;
        }

        public void Set(StarReward r, bool reached, bool claimed)
        {
            starsText.text = r.Stars.ToString();
            icon.sprite = ArtLibrary.S(r.Icon);
            icon.preserveAspect = true;
            caption.text = r.Caption;
            bool ready = reached && !claimed;
            claimButton.gameObject.SetActive(ready);
            done.SetActive(claimed);
            lockIcon.SetActive(!reached);
            // ещё не открытая награда — приглушённая, полученная — чуть бледнее готовой
            var tint = reached ? Color.white : new Color(0.72f, 0.68f, 0.64f, 1f);
            card.color = tint;
            icon.color = claimed ? new Color(1f, 1f, 1f, 0.55f) : tint;
            Tween.Kill(card.transform);
            card.transform.localScale = Vector3.one;
            if (ready)
            {
                // готовая награда «дышит», чтобы её было видно среди остальных
                var tr = card.transform;
                Tween.Run(tr, Tween.ChScale, 1000f, Ease.Linear, k =>
                {
                    if (tr != null) tr.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(k * 1000f * 3f)) * 0.06f);
                });
            }
        }
    }
}

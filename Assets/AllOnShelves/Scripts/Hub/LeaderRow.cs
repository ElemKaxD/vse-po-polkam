using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Строка рейтинга (01.10.2026): медаль или номер места, аватарка-зверёк в рамке, ник, очки, подарок за место.
    /// Ник — в одну строку: уменьшается до 22, длиннее — обрезается «…».
    /// </summary>
    public class LeaderRow : MonoBehaviour
    {
        public TextMeshProUGUI rank;
        public Image medal;              // lb_medal_1..3 или пустой кружок lb_place под номер
        public Image avatar;
        public Image frame;
        public TextMeshProUGUI playerName;
        public TextMeshProUGUI score;
        public Image scoreIcon;
        public Image gift;
        public Image background;

        public static readonly Color Normal = Color.white;
        public static readonly Color Me = new Color(1f, 0.74f, 0.64f);      // строка самого игрока — коралловая
        public static readonly Color Top = new Color(1f, 0.9f, 0.58f);      // места 1–3 на вкладке «Рядом»

        public void Set(int r, string n, int s, Color bg, string avatarSprite, string frameSprite, string scoreSprite, int giftTier)
        {
            gameObject.SetActive(true);
            bool medalPlace = r >= 1 && r <= 3;
            medal.sprite = ArtLibrary.S(medalPlace ? "lb_medal_" + r : "lb_place");
            medal.preserveAspect = true;
            rank.gameObject.SetActive(!medalPlace);
            rank.text = r > 0 ? r.ToString() : "—";
            playerName.text = string.IsNullOrEmpty(n) ? "Гость" : n;
            score.text = s.ToString();
            SetSprite(avatar, avatarSprite);
            SetSprite(frame, frameSprite);
            SetSprite(scoreIcon, scoreSprite);
            if (gift != null)
            {
                gift.gameObject.SetActive(giftTier > 0);
                if (giftTier > 0) SetSprite(gift, "lb_gift_" + giftTier);
            }
            background.color = bg;
        }

        static void SetSprite(Image img, string sprite)
        {
            if (img == null) return;
            var sp = string.IsNullOrEmpty(sprite) ? null : ArtLibrary.S(sprite);
            img.gameObject.SetActive(sp != null);
            if (sp != null) { img.sprite = sp; img.preserveAspect = true; }
        }
    }
}

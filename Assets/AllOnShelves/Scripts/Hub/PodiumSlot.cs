using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Место на пьедестале рейтинга: зверёк в нарисованном кольце, медаль, ник на табличке, очки.</summary>
    public class PodiumSlot : MonoBehaviour
    {
        public Image avatar;
        public Image medal;
        public TextMeshProUGUI playerName;
        public TextMeshProUGUI score;
        public Image scoreIcon;
        public GameObject scorePill;

        public void Set(string n, int s, string avatarSprite, string scoreSprite)
        {
            bool has = !string.IsNullOrEmpty(avatarSprite);
            avatar.gameObject.SetActive(has);
            medal.gameObject.SetActive(has);
            scorePill.SetActive(has);
            playerName.text = has ? n : "—";
            if (!has) return;
            avatar.sprite = ArtLibrary.S(avatarSprite);
            avatar.preserveAspect = true;
            score.text = s.ToString();
            var sp = ArtLibrary.S(scoreSprite);
            if (sp != null) { scoreIcon.sprite = sp; scoreIcon.preserveAspect = true; }
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Ценник-уровень на карте (кадр 10). 200 штук лежат в сцене Hub, их можно двигать.</summary>
    public class MapNode : MonoBehaviour
    {
        public int levelId;
        public Image tag;
        public TextMeshProUGUI number;
        public Image[] stars = new Image[3];
        public GameObject newBadge;
        public Button button;

        static readonly Color[] ModeTint = { Color.white, new Color(1f, 0.80f, 0.55f), new Color(1f, 0.62f, 0.58f) };

        public void Refresh(GameApp app, Sprite normal, Sprite hard, Sprite superhard, Sprite revision, Sprite locked, Sprite starOn, Sprite starOff)
        {
            var lvl = app.Level(levelId);
            // прогресс и звёзды — текущего режима сложности (v4)
            int reached = app.Save.Reached(app.Mode);
            bool unlocked = levelId <= reached + 1;
            bool done = levelId <= reached;
            string diff = lvl?.difficulty ?? "easy";
            Sprite s = !unlocked ? locked : lvl != null && lvl.revision ? revision : diff == "superhard" ? superhard : diff == "hard" ? hard : normal;
            tag.sprite = s;
            number.text = levelId.ToString();
            number.gameObject.SetActive(lvl == null || !lvl.revision || !unlocked);
            // звёзды на ценнике нарисованы серыми/жёлтыми «пустышками»: поверх них кладём заработанные и пустые
            int st = app.Save.GetStars(levelId);
            bool overlay = unlocked && s != revision && s != superhard;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(overlay);
                stars[i].sprite = i < st ? starOn : starOff;
            }
            newBadge.SetActive(unlocked && !done && lvl != null && !string.IsNullOrEmpty(lvl.mechanic));
            button.interactable = unlocked;
            // закрытые ценники не полупрозрачные — только серые, номер тёмный, чтобы читался на карте
            var cols = button.colors; cols.disabledColor = Color.white; button.colors = cols;
            // режим сложности — цвет ценника: Средний — оранжевый, Сложный — красный (закрытые остаются серыми)
            tag.color = !unlocked || app.Mode == 0 ? Color.white : ModeTint[app.Mode];
            number.color = unlocked ? Color.white : new Color32(0x6B, 0x5E, 0x57, 255);
        }
    }
}

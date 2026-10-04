using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Сцена одного магазина в ремонте (кадры 08–09): фон, здание «до/после», предметы ремонта.
    /// Предметы — дочерние объекты с именем = id предмета (meta_d01_sign), стоят на земле или висят на здании.
    /// Их можно двигать в сцене. Навесные видны на всех этапах, наземные — только на своём этапе,
    /// чтобы перед магазином не было толкучки. Предметы, нарисованные «целым магазином», в сцене не стоят.
    /// </summary>
    public class RenoStore : MonoBehaviour
    {
        public string storeId;
        public int firstDistrict;
        public Image background;
        public Image buildingWorn;
        public Image buildingNew;
        public RectTransform sparkle;
        public Image[] items = new Image[0];
        public int[] itemDistrict = new int[0];
        public bool[] itemAttached = new bool[0];

        public Image Find(string id)
        {
            foreach (var i in items) if (i != null && i.name == id) return i;
            return null;
        }

        public void Refresh(GameApp app, int viewDistrict)
        {
            // здание обновляется, когда закончен первый этап этого магазина
            bool renovated = app.StageComplete(firstDistrict);
            buildingWorn.gameObject.SetActive(!renovated);
            buildingNew.gameObject.SetActive(renovated);
            for (int k = 0; k < items.Length; k++)
            {
                var img = items[k];
                if (img == null) continue;
                bool show = app.Save.boughtItems.Contains(img.name) && (itemAttached[k] || itemDistrict[k] == viewDistrict);
                img.gameObject.SetActive(show);
            }
        }

        /// <summary>Вспышка на здании — для покупок, которые видны только на карточке.</summary>
        public void Celebrate()
        {
            if (sparkle == null) return;
            sparkle.gameObject.SetActive(true);
            sparkle.localScale = Vector3.zero;
            var g = sparkle.GetComponent<Image>();
            if (g != null) g.color = Color.white;
            Tween.Scale(sparkle, Vector3.one * 1.3f, 0.35f, Ease.OutBack);
            if (g != null) Tween.Fade(g, 0f, 0.5f, () => sparkle.gameObject.SetActive(false), 0.35f);
        }
    }
}

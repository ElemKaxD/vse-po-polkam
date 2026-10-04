using UnityEngine;

namespace AllOnShelves
{
    /// <summary>
    /// Слой фиксированного размера (например, ценники карты в координатах картинки 1920×1080)
    /// масштабируется вместе с родителем, сохраняя пропорции: позиции детей совпадают с картинкой.
    /// cover = false — слой целиком вписан в родителя; cover = true — накрывает родителя целиком
    /// (лишнее по краям обрезается): так кадр во весь экран не оставляет полос на экранах не 16:9.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ScaleToParent : MonoBehaviour
    {
        public Vector2 reference = new Vector2(1920f, 1080f);
        public bool cover;
        // для страниц-картинок с кнопками (магазин, рейтинг, 01.10.2026): «накрыть» экран, но не больше чем
        // в maxOver раз от «вписать» — иначе на широком экране обрезаются вкладки сверху и карточки снизу.
        // Полосы по бокам закрывает приглушённая копия фона. 0 — без ограничения.
        public float maxOver;

        void OnEnable() => Apply();
        void OnRectTransformDimensionsChange() => Apply();
        void Update() => Apply();

        public void Apply()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || reference.x <= 0f || reference.y <= 0f) return;
            var size = parent.rect.size;
            float kx = size.x / reference.x, ky = size.y / reference.y;
            float k = cover ? Mathf.Max(kx, ky) : Mathf.Min(kx, ky);
            if (cover && maxOver > 0f) k = Mathf.Min(k, Mathf.Min(kx, ky) * maxOver);
            if (k <= 0f) return;
            var rt = (RectTransform)transform;
            rt.sizeDelta = reference;
            var s = new Vector3(k, k, 1f);
            if (transform.localScale != s) transform.localScale = s;
        }
    }
}

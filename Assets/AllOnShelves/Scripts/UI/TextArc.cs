using TMPro;
using UnityEngine;

namespace AllOnShelves.UI
{
    /// <summary>
    /// Гнёт надпись дугой, чтобы она повторяла форму ленты: середина приподнята,
    /// крайние буквы наклонены по касательной. Прямая строка на изогнутой ленте выглядит наклейкой.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextArc : MonoBehaviour
    {
        [Tooltip("Подъём середины строки, в точках канвы")]
        public float bend = 14f;
        [Tooltip("Наклон крайних букв, градусов")]
        public float tilt = 6f;

        TextMeshProUGUI _text;
        string _was;
        Vector2 _wasSize;
        float _wasFont;

        void OnEnable()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _was = null;
        }

        void LateUpdate()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text == null) return;
            // пересчитываем, только когда что-то изменилось: текст, размер рамки или подобранный кегль
            var size = _text.rectTransform.rect.size;
            if (_text.text == _was && size == _wasSize && Mathf.Approximately(_text.fontSize, _wasFont)) return;
            _was = _text.text;
            _wasSize = size;
            _wasFont = _text.fontSize;
            Apply();
        }

        public void Apply()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text == null || string.IsNullOrEmpty(_text.text)) return;
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            if (info.characterCount == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (!info.characterInfo[i].isVisible) continue;
                minX = Mathf.Min(minX, info.characterInfo[i].bottomLeft.x);
                maxX = Mathf.Max(maxX, info.characterInfo[i].topRight.x);
            }
            float width = maxX - minX;
            if (width <= 1f) return;

            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                int v = ch.vertexIndex;
                float mid = (ch.bottomLeft.x + ch.topRight.x) * 0.5f;
                float t = (mid - minX) / width;              // 0 слева, 1 справа
                float s = 2f * t - 1f;                       // -1 … 1
                float lift = bend * (1f - s * s);            // парабола: 0 по краям, bend в середине
                var pivot = new Vector3(mid, (ch.bottomLeft.y + ch.topRight.y) * 0.5f, 0f);
                var rot = Quaternion.Euler(0f, 0f, -tilt * s);
                for (int j = 0; j < 4; j++)
                    verts[v + j] = pivot + rot * (verts[v + j] - pivot) + new Vector3(0f, lift, 0f);
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Подстраивает CanvasScaler под пропорции экрана (ГДД 3.2): на широких экранах — по высоте,
    /// на узких и вертикальных — по ширине, чтобы поле 16:9 всегда целиком помещалось.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveScaler : MonoBehaviour
    {
        const float DesignAspect = 16f / 9f;
        CanvasScaler _scaler;
        Vector2Int _last;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != _last.x || Screen.height != _last.y) Apply();
        }

        void Apply()
        {
            _last = new Vector2Int(Screen.width, Screen.height);
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : DesignAspect;
            _scaler.matchWidthOrHeight = aspect >= DesignAspect ? 1f : 0f;
        }
    }
}

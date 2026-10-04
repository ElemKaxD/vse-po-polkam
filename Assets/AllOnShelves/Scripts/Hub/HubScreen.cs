using UnityEngine;

namespace AllOnShelves.Hub
{
    /// <summary>Базовый экран хаба. Экраны — дочерние объекты Canvas сцены Hub.</summary>
    public class HubScreen : MonoBehaviour
    {
        public CanvasGroup group;
        [HideInInspector] public HubController hub;

        public bool IsOpen => gameObject.activeSelf;

        public virtual void Open()
        {
            gameObject.SetActive(true);
            if (group != null)
            {
                group.alpha = 0f;
                Tween.Fade(group, 1f, 0.2f);
            }
            Refresh();
        }

        public virtual void Close()
        {
            gameObject.SetActive(false);
        }

        public virtual void Refresh() { }
    }
}

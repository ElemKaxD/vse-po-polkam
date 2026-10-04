using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Комната Торгового дома в кадре FrameW×FrameH: голая комната room_NN_base и купленный декор поверх неё
    /// (куски вырезаны из того же кадра — Tools/ArtPipeline/room_build.py, RoomLegoData). Им рисуются и экран
    /// комнаты, и превью в окне этажа.
    /// </summary>
    public class RoomView : MonoBehaviour
    {
        public Image baseImage;
        public RectTransform piecesRoot;

        readonly List<Image> _pieces = new List<Image>();
        readonly List<int> _pieceItem = new List<int>();
        int _room = -1;

        public static Vector2 Frame => new Vector2(RoomLegoData.FrameW, RoomLegoData.FrameH);

        /// <summary>Комната room (0..11), куплено decor предметов. Возвращает, изменилась ли комната.</summary>
        public void Show(int room, int decor)
        {
            if (_room != room) Build(room);
            for (int i = 0; i < _pieces.Count; i++) _pieces[i].gameObject.SetActive(_pieceItem[i] <= decor);
        }

        void Build(int room)
        {
            _room = room;
            baseImage.sprite = ArtLibrary.S($"room_{room + 1:00}_base");
            var parts = room < RoomLegoData.Rooms.Length ? RoomLegoData.Rooms[room] : new RoomLegoData.Piece[0];
            while (_pieces.Count < parts.Length)
            {
                var go = new GameObject("Piece", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(piecesRoot, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _pieces.Add(img);
                _pieceItem.Add(0);
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                bool on = i < parts.Length;
                _pieces[i].gameObject.SetActive(on);
                if (!on) { _pieceItem[i] = 99; continue; }
                var p = parts[i];
                _pieceItem[i] = p.Item;
                _pieces[i].sprite = ArtLibrary.S(p.Sprite);
                _pieces[i].rectTransform.anchoredPosition = Center(p);
                _pieces[i].rectTransform.sizeDelta = new Vector2(p.W, p.H);
                _pieces[i].rectTransform.localScale = Vector3.one;
                _pieces[i].color = Color.white;
            }
        }

        /// <summary>Центр куска в координатах кадра (от центра, вверх — плюс).</summary>
        public static Vector2 Center(RoomLegoData.Piece p) =>
            new Vector2(p.X + p.W / 2f - RoomLegoData.FrameW / 2f, RoomLegoData.FrameH / 2f - (p.Y + p.H / 2f));

        /// <summary>Где в кадре стоит предмет item (1..6): центр самого крупного куска.</summary>
        public static Vector2 ItemCenter(int room, int item)
        {
            var best = default(RoomLegoData.Piece);
            int area = -1;
            foreach (var p in RoomLegoData.Rooms[room])
                if (p.Item == item && p.W * p.H > area) { area = p.W * p.H; best = p; }
            return area < 0 ? Vector2.zero : Center(best);
        }

        /// <summary>
        /// Где поставить жильца (x центра; ноги — на feetY): место на полу, где он меньше всего закрывает декор.
        /// При равенстве — ближе к середине. Края кадра заняты карточкой сотрудника и кассой — туда не ставим.
        /// </summary>
        public static float StaffX(int room, Vector2 size, float feetY)
        {
            float best = 0f, bestCost = float.MaxValue;
            var parts = room < RoomLegoData.Rooms.Length ? RoomLegoData.Rooms[room] : new RoomLegoData.Piece[0];
            for (float x = -420f; x <= 420f; x += 30f)
            {
                // тело — середина картинки (по бокам у зверей хвосты, лапы и воздух)
                var body = new Rect(x - size.x * 0.35f, feetY, size.x * 0.7f, size.y * 0.9f);
                float cost = Mathf.Abs(x) * 40f;
                foreach (var p in parts)
                {
                    var c = Center(p);
                    var r = new Rect(c.x - p.W / 2f, c.y - p.H / 2f, p.W, p.H);
                    float w = Mathf.Min(body.xMax, r.xMax) - Mathf.Max(body.xMin, r.xMin);
                    float h = Mathf.Min(body.yMax, r.yMax) - Mathf.Max(body.yMin, r.yMin);
                    if (w > 0f && h > 0f) cost += w * h;
                }
                if (cost < bestCost) { bestCost = cost; best = x; }
            }
            return best;
        }

        /// <summary>Самый крупный кусок предмета — им предмет показан на плитке.</summary>
        public static string ItemIcon(int room, int item)
        {
            string best = null;
            int area = -1;
            foreach (var p in RoomLegoData.Rooms[room])
                if (p.Item == item && p.W * p.H > area) { area = p.W * p.H; best = p.Sprite; }
            return best;
        }

        /// <summary>Куски предмета item — для анимации «встал на место».</summary>
        public IEnumerable<Image> PiecesOf(int item)
        {
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieceItem[i] == item && _pieces[i].gameObject.activeSelf) yield return _pieces[i];
        }
    }
}

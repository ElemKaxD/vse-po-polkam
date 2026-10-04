using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Панель заказа (цели уровня).</summary>
    public class GoalPanelView : MonoBehaviour
    {
        public const int MaxRows = 10;
        /// <summary>Планшет с зажимом — целиком, в родных пропорциях.</summary>
        public const string Sprite = "ui_panel_clipboard_flat2d_generated";
        public const float Head = 104f;   // зажим планшета: выше него бумаги нет (0.19 высоты картинки)
        public const float TitleH = 56f;  // строка «Заказ» на самом верху бумаги
        public const float Step = 72f;    // шаг строк при небольшом заказе
        public const float Tail = 62f;    // нижний край бумаги (0.11 высоты картинки)

        public GoalRow[] rows = new GoalRow[MaxRows];
        public RectTransform panel;       // планшет: размер не трогаем, подгоняем список под него

        /// <summary>С этого числа целей строки идут в две колонки: в одну они мельчали до нечитаемых.</summary>
        const int TwoColumnsFrom = 6;
        /// <summary>Центры колонок и сдвиг «тела» строки (значок + число) от её середины.</summary>
        const float LeftX = -62f, RightX = 88f, RowBodyX = -28f;   // бумага планшета смещена вправо от центра картинки

        float _k = 1f;
        Vector2[] _checkHome;

        public void Build(LevelState s)
        {
            int n = Mathf.Clamp(s.GoalTypes.Length, 1, rows.Length);
            if (_checkHome == null)
            {
                _checkHome = new Vector2[rows.Length];
                for (int i = 0; i < rows.Length; i++) _checkHome[i] = rows[i].check.rectTransform.anchoredPosition;
            }
            // список подгоняем под планшет: при многих целях — две колонки, шаг и размер строк уменьшаются
            bool two = n >= TwoColumnsFrom;
            int perCol = two ? (n + 1) / 2 : n;
            float inner = panel != null ? panel.sizeDelta.y - Head - TitleH - Tail : perCol * Step;
            float step = Mathf.Min(Step, inner / perCol);
            float k = two ? Mathf.Min(0.85f, step / Step) : step / Step;
            _k = k;
            for (int i = 0; i < rows.Length; i++)
            {
                bool on = i < n;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var rt = (RectTransform)rows[i].transform;
                int col = two ? i / perCol : 0, row = two ? i % perCol : i;
                float x = two ? (col == 0 ? LeftX : RightX) - RowBodyX * k : 0f;
                rt.anchoredPosition = new Vector2(x, -Head - TitleH - step * (row + 0.5f));
                rt.localScale = Vector3.one * k;
                // в две колонки галочка не помещается справа — ставим её на уголок значка
                var check = rows[i].check.rectTransform;
                check.anchoredPosition = two ? rows[i].icon.rectTransform.anchoredPosition + new Vector2(22f, -18f) : _checkHome[i];
                check.localScale = Vector3.one * (two ? 0.8f : 1f);
                rows[i].icon.sprite = ArtLibrary.Item(s.GoalTypes[i]);
                ArtLibrary.Fit(rows[i].icon, 54f);   // у каждого товара свои пропорции
            }
            Refresh(s, false);
        }

        public void Refresh(LevelState s, bool animate)
        {
            for (int i = 0; i < s.GoalTypes.Length && i < rows.Length; i++)
            {
                var r = rows[i];
                int done = Mathf.Min(s.GoalDone[i], s.GoalNeed[i]);
                string text = $"{done}/{s.GoalNeed[i]}";
                bool changed = r.text.text != text;
                r.text.text = text;
                bool complete = done >= s.GoalNeed[i];
                r.check.gameObject.SetActive(complete);
                if (animate && changed) Tween.Punch(r.transform, 0.25f, 0.3f, _k);
            }
        }

        public Vector3 RowWorld(int type, LevelState s)
        {
            int g = s.GoalIndex(type);
            return g >= 0 && g < rows.Length ? rows[g].transform.position : transform.position;
        }
    }
}

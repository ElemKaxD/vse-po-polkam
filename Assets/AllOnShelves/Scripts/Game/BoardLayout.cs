using System;
using UnityEngine;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Раскладка игрового поля: где стоят ленты, стеллаж и тележка (отдельно для уровней с одной и двумя лентами).
    /// Лежит в Resources/BoardLayout.asset и не перезаписывается сборкой сцен — значения можно менять мышкой:
    /// окно «Всё по полкам → Просмотр сцен и уровней», подвинуть ленту/стеллаж/тележку в сцене Game,
    /// кнопка «Запомнить положение поля».
    /// </summary>
    [CreateAssetMenu(menuName = "Всё по полкам/Board Layout")]
    public class BoardLayout : ScriptableObject
    {
        [Serializable]
        public class Config
        {
            public Vector2 belt1Pos = new Vector2(0f, 350f);
            public Vector2 belt2Pos = new Vector2(0f, 275f);
            public float beltScale = 1f;
            public Vector2 shelfPos = new Vector2(0f, 20f);
            public float shelfScale = 0.9f;
            public Vector2 cartPos = new Vector2(0f, -382f);
            public float cartScale = 0.92f;
        }

        [Header("Одна лента")] public Config oneBelt = new Config();

        [Header("Две ленты")]
        public Config twoBelts = new Config
        {
            belt1Pos = new Vector2(0f, 400f), belt2Pos = new Vector2(0f, 275f),
            shelfPos = new Vector2(0f, -25f), shelfScale = 0.82f,
        };

        static BoardLayout _default;

        public static BoardLayout Default
        {
            get
            {
                if (_default == null) _default = Resources.Load<BoardLayout>("BoardLayout");
                if (_default == null) _default = CreateInstance<BoardLayout>();
                return _default;
            }
        }
    }
}

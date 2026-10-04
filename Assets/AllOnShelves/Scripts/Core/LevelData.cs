using System;

namespace AllOnShelves.Core
{
    // Сериализуемые данные уровней (JSON, ГДД 6.6). Читаются через UnityEngine.JsonUtility в игровом слое.

    [Serializable]
    public class LevelSet
    {
        public int version = 1;
        public LevelData[] levels;
    }

    [Serializable]
    public class LevelData
    {
        public int id;
        public int district = 1;
        /// <summary>tutorial / easy / medium / hard / superhard</summary>
        public string difficulty = "easy";
        public int cart = 5;
        public int window = 3;
        public int visible = 8;
        public SectionData[] sections = new SectionData[0];
        public GoalData[] goals = new GoalData[0];
        public BeltData[] belts = new BeltData[0];
        public CustomerData[] customers = new CustomerData[0];
        /// <summary>Сколько ходов с ленты скоропорт живёт в тележке. 0 — механика выключена.</summary>
        public int perishTimer;
        /// <summary>Цикл дверцы морозилки: открыто N ходов, закрыто M. 0 — всегда открыта.</summary>
        public int doorOpen;
        public int doorClosed;
        public bool night;
        public bool revision;
        public string[] cartStart = new string[0];
        /// <summary>Идентификатор механики, которой обучает уровень (карточка «Новое!»).</summary>
        public string mechanic = "";
        /// <summary>Сценарий обучения (tut_*), пусто — без обучения.</summary>
        public string tutorial = "";
        public MoveData[] solution = new MoveData[0];
        public LevelMetrics metrics = new LevelMetrics();
    }

    [Serializable]
    public class SectionData
    {
        /// <summary>normal / freezer / sale</summary>
        public string kind = "normal";
        public int lockSets;
        public string prefillType = "";
        public int prefillCount;
    }

    [Serializable]
    public class GoalData
    {
        public string type;
        public int sets;
    }

    [Serializable]
    public class BeltData
    {
        /// <summary>Токены: "apple", "box:apple", "pallet:apple", "bundle:apple+milk".</summary>
        public string[] tokens = new string[0];
    }

    [Serializable]
    public class CustomerData
    {
        /// <summary>Номер хода с ленты, после которого приходит покупатель.</summary>
        public int atMove;
        public string type;
        public int patience = 4;
        /// <summary>0..5 — кролик, кот, лиса, ёжик, собака, панда.</summary>
        public int look;
    }

    [Serializable]
    public class MoveData
    {
        public int sk, sb, si, dk, di;
    }

    [Serializable]
    public class LevelMetrics
    {
        public int minPeakCart = -1;
        public bool greedyWin;
        public float randomWinRate;
        public int moves;
        public int seed;
    }
}

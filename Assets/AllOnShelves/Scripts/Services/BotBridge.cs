using System;
using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;
using AllOnShelves.Game;
using UnityEngine;

namespace AllOnShelves
{
#if UNITY_WEBGL && !UNITY_EDITOR
    static class BotBridgeJs
    {
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void SendResult(string json);
    }
#endif

    /// <summary>
    /// Мост для бота в билде (BuildBot): страница вызывает SendMessage("BotBridge","Cmd",json),
    /// результат уходит в JS через BotBridge.jslib (window.botResult). Профили игры:
    /// optimal — по решению солвера; casual — сильный, но иногда ошибается;
    /// granny — «бабушка»: медленно, случайно среди допустимых, любит заполнять тележку.
    /// </summary>
    public class BotBridge : MonoBehaviour
    {
        public static BotBridge I { get; private set; }
        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            name = "BotBridge";
        }

        class CmdMsg
        {
            public string Key;
            public Dictionary<string, string> Args = new Dictionary<string, string>();
            public string Str(string k, string d) { return Args.TryGetValue(k, out var v) ? v : d; }
            public int Int(string k) { return Args.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0; }
        }

        /// <summary>Вызов из JS: SendMessage("BotBridge", "Cmd", json).</summary>
        public void Cmd(string json)
        {
            try
            {
                var cmd = ParseCmd(json);
                switch (cmd.Key)
                {
                    case "ping":
                        Reply("{\"ok\":true}");
                        break;
                    case "state":
                        Reply(State());
                        break;
                    case "play":
                        GameApp.I.PlayLevel(cmd.Int("level"));
                        Reply("{\"ok\":true}");
                        break;
                    case "move":
                        Reply(MoveOnce(cmd.Str("policy", "optimal")));
                        break;
                    case "hub":
                        GameApp.I.GoHub();
                        Reply("{\"ok\":true}");
                        break;
                    default:
                        Reply("{\"error\":\"unknown cmd\"}");
                        break;
                }
            }
            catch (Exception e)
            {
                Reply("{\"error\":\"" + e.Message.Replace("\"", "'") + "\"}");
            }
        }

        static CmdMsg ParseCmd(string json)
        {
            var m = new CmdMsg();
            foreach (var part in json.Trim().Trim('{', '}').Split(','))
            {
                var kv = part.Split(new[] { ':' }, 2);
                if (kv.Length != 2) continue;
                m.Args[kv[0].Trim().Trim('"')] = kv[1].Trim().Trim('"');
            }
            m.Args.TryGetValue("cmd", out var k);
            m.Key = k ?? "";
            m.Args.Remove("cmd");
            return m;
        }

        static GameController Gc() => FindFirstObjectByType<GameController>();

        static string State()
        {
            var gc = Gc();
            if (gc == null || gc.DebugState == null) return "{\"scene\":\"hub\"}";
            var st = gc.DebugState;
            return "{\"scene\":\"game\",\"result\":\"" + st.Result +
                   "\",\"moves\":" + st.Moves +
                   ",\"cart\":" + st.Cart.Count +
                   ",\"belts\":" + (st.Belts != null ? st.Belts.Length : 0) + "}";
        }

        /// <summary>Один ход выбранным профилем.</summary>
        string MoveOnce(string policy)
        {
            var gc = Gc();
            if (gc == null) return "{\"error\":\"no game\"}";
            // обучение, блокирующее ввод (tut_goal), бот «нажимает» как игрок
            var tut = FindFirstObjectByType<Game.TutorialOverlay>();
            if (tut != null && tut.Visible) tut.TapNow();
            if (gc.DebugStartShift()) return "{\"moved\":false,\"shift\":true}";
            var st = gc.DebugState;
            if (st == null || st.Result != GameResult.Playing) return "{\"done\":true,\"result\":\"" + st.Result + "\"}";

            bool moved;
            switch (policy)
            {
                case "granny": moved = MoveHumanLike(0.35f); break;
                case "casual": moved = _rng.NextDouble() < 0.12d ? MoveHumanLike(0.5f) : gc.DebugAutoMove(); break;
                default: moved = gc.DebugAutoMove(); break;
            }
            st = gc.DebugState;
            return "{\"moved\":" + (moved ? "true" : "false") +
                   ",\"moves\":" + st.Moves +
                   ",\"result\":\"" + st.Result + "\"}";
        }

        /// <summary>Случайный допустимый ход — как играет человек без плана.</summary>
        bool MoveHumanLike(float cartBias)
        {
            var gc = Gc();
            var st = gc.DebugState;
            var legal = Rules.LegalMoves(st);
            if (legal.Count == 0) return false;
            var pick = legal[_rng.Next(legal.Count)];
            if (_rng.NextDouble() < cartBias)
            {
                var toCart = legal.Where(m => m.Dst == TargetKind.Cart).ToList();
                if (toCart.Count > 0) pick = toCart[_rng.Next(toCart.Count)];
            }
            return gc.DebugApplyMove(pick);
        }

        void Reply(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BotBridgeJs.SendResult(json);
#else
            Debug.Log("[BotBridge] " + json);
#endif
        }
    }

}

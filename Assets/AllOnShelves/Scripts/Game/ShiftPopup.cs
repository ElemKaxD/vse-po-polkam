using System;
using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Смена перед уровнем. v2 (03.10.2026, по концепту shift_empty): сверху крупные значки «что ждёт в уровне»
    /// (нажал — описание внизу), по центру три больших места на смене, под ними «Рекомендация» (игра ставит
    /// тех, кто поможет), внизу строка-описание и «Начать!». «+» в пустом месте открывает окно «Команда»
    /// (StaffPickerPopup) — там игрок выбирает сам. Раньше вся команда сидела в этом же окне, и всё было мелким.
    /// </summary>
    public class ShiftPopup : Popup
    {
        public TextMeshProUGUI title;
        public RectTransform featuresRoot;
        public Image[] featureIcons = new Image[5];
        public Button[] featureButtons = new Button[5];
        [OptionalRef] public GameObject[] featureSockets;   // золотое гнездо ui_mech_socket под значком (v4)
        [OptionalRef] public TextMeshProUGUI noFeatures;
        public Button[] slots = new Button[3];
        public Image[] slotFaces = new Image[3];
        public GameObject[] slotPlus = new GameObject[3];
        public GameObject[] slotLocks = new GameObject[3];
        public TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[3];
        public Image[] slotStars = new Image[12];          // по 4 звезды уровня в каждом месте
        public TextMeshProUGUI infoText;
        public Button recommendButton;
        public Button startButton;
        public StaffPickerPopup picker;                   // окно «Команда»
        public StaffCardView[] cards = new StaffCardView[10];
        public Sprite cardNormal, cardChosen;

        LevelState _st;
        readonly List<string> _chosen = new List<string>();
        List<string> _advice = new List<string>();
        List<string> _features = new List<string>();
        Action<List<string>> _onStart;
        int _pickSlot = -1;

        /// <summary>Сотрудник встал на смену или ушёл с неё — для обучения.</summary>
        public event Action<string> Picked;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                c.button.onClick.AddListener(() => Tap(c, fromPicker: true));
            }
            for (int i = 0; i < slots.Length; i++)
            {
                int k = i;
                slots[i].onClick.AddListener(() => TapSlot(k));
            }
            for (int i = 0; i < featureButtons.Length; i++)
            {
                int k = i;
                if (featureButtons[i] != null) featureButtons[i].onClick.AddListener(() => TapFeature(k));
            }
            recommendButton.onClick.AddListener(Recommend);
            startButton.onClick.AddListener(Go);
        }

        public IReadOnlyList<string> Advice => _advice;

        /// <param name="preset">кого поставить сразу (повтор того же уровня — прежняя смена игрока)</param>
        public void Open(LevelState s, string levelTitle, IEnumerable<string> preset, Action<List<string>> onStart)
        {
            _st = s;
            _onStart = onStart;
            _advice = Team.Recommend(s);
            _chosen.Clear();
            if (preset != null)
                foreach (var id in preset)
                    if (_chosen.Count < Team.Slots && House.LevelOf(id) > 0 && !_chosen.Contains(id)) _chosen.Add(id);
            title.text = levelTitle;
            ShowFeatures();
            Refresh();
            infoText.text = (_features.Count == 0 ? "Обычный уровень — без сюрпризов. " : "Нажми на значок сверху — расскажу, что это. ") +
                            "«+» — выбрать сотрудника самому, «Рекомендация» — поставлю тех, кто поможет.";
            if (picker != null) picker.HideInstant();
            Show();
        }

        public override void HideInstant()
        {
            base.HideInstant();
            if (picker != null) picker.HideInstant();
        }

        void ShowFeatures()
        {
            _features = Team.Features(_st);
            if (noFeatures != null) noFeatures.gameObject.SetActive(_features.Count == 0);
            for (int i = 0; i < featureIcons.Length; i++)
            {
                bool on = i < _features.Count;
                featureIcons[i].gameObject.SetActive(on);
                if (featureSockets != null && i < featureSockets.Length && featureSockets[i] != null) featureSockets[i].SetActive(on);
                if (!on) continue;
                var info = MechanicPopup.Info(_features[i]);
                featureIcons[i].sprite = ArtLibrary.S("icon_mech_" + _features[i]) ?? ArtLibrary.S(info.sprite);   // набор PROMPTS_v3.3
                featureIcons[i].preserveAspect = true;
            }
        }

        /// <summary>Нажали на значок механики — что это, одной-двумя фразами.</summary>
        void TapFeature(int i)
        {
            if (i >= _features.Count) return;
            AudioService.Play("sfx_button");
            var info = MechanicPopup.Info(_features[i]);
            infoText.text = info.title + ": " + FirstSentences(info.text, 2);
            Tween.Punch(featureIcons[i].transform, 0.15f, 0.3f);
        }

        static string FirstSentences(string text, int n)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int pos = 0;
            for (int k = 0; k < n; k++)
            {
                int dot = text.IndexOf(". ", pos, StringComparison.Ordinal);
                if (dot < 0) return text;
                pos = dot + 1;
            }
            return text.Substring(0, pos);
        }

        void Refresh()
        {
            int open = Team.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                bool locked = i >= open;
                string id = i < _chosen.Count ? _chosen[i] : null;
                slotLocks[i].SetActive(locked);
                slotPlus[i].SetActive(!locked && id == null);
                slotFaces[i].gameObject.SetActive(id != null);
                if (id != null) { slotFaces[i].sprite = ArtLibrary.S(Team.Face(id)); slotFaces[i].preserveAspect = true; }
                int lvl = id != null ? House.LevelOf(id) : 0;
                for (int k = 0; k < 4; k++)
                {
                    var star = slotStars[i * 4 + k];
                    if (star == null) continue;
                    star.gameObject.SetActive(id != null);
                    star.sprite = ArtLibrary.S(k < lvl ? "ui_star_big" : "ui_star_big_empty");
                }
                slotTexts[i].gameObject.SetActive(locked);
                if (locked) slotTexts[i].text = i == 1 ? "Весь 2-й этаж" : "Буфет";
            }
            for (int i = 0; i < cards.Length && i < House.Staff.Length; i++)
            {
                var c = cards[i];
                var st = House.Staff[i];
                c.id = st.Id;
                int lvl = House.LevelOf(st.Id);
                bool ready = lvl > 0, chosen = _chosen.Contains(st.Id);
                c.face.sprite = ArtLibrary.S(Team.Face(st.Id));
                c.face.preserveAspect = true;
                c.face.color = ready ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.85f);
                c.nameText.text = st.Name.Split('-')[0];
                for (int k = 0; k < c.stars.Length; k++)
                {
                    c.stars[k].gameObject.SetActive(ready);
                    c.stars[k].sprite = ArtLibrary.S(k < lvl ? "ui_star_big" : "ui_star_big_empty");
                }
                c.lockMark.SetActive(!ready);
                if (!ready) c.lockText.text = WhenComes(st);
                c.recommend.SetActive(ready && _advice.Contains(st.Id));
                c.onShift.SetActive(chosen);
                c.bg.sprite = chosen && cardChosen != null ? cardChosen : cardNormal;
            }
            startButton.interactable = true;
            recommendButton.interactable = Team.Ready().Any();
        }

        /// <summary>Когда придёт сотрудник, которого ещё нет.</summary>
        static string WhenComes(StaffInfo st)
        {
            if (st.Gold && !GameApp.I.Save.goldPath) return "Золотой путь";
            int room = House.RoomOf(st.Id);
            if (room < 0) return "";
            var state = House.State(room);
            if (state == RoomState.Locked) return "Ур. " + House.All[room].UnlockLevel;
            return state == RoomState.Ready ? "Заселить" : "Нет комнаты";
        }

        /// <summary>Место на смене: пустое — открыть «Команду», занятое — убрать сотрудника, закрытое — когда откроется.</summary>
        void TapSlot(int i)
        {
            if (i >= Team.Slots)
            {
                AudioService.Play("sfx_nope");
                Toast.Show(i == 1 ? "Второе место откроется, когда построишь весь 2-й этаж" : "Третье место — в комнате «Буфет»", "icon_lock");
                return;
            }
            AudioService.Play("sfx_button");
            if (i < _chosen.Count)
            {
                _chosen.RemoveAt(i);
                Refresh();
                Picked?.Invoke(null);
                return;
            }
            _pickSlot = i;
            Refresh();
            if (picker == null) return;
            picker.hintText.text = _advice.Count > 0 ? "«Советую» — у тех, чьё умение работает в этом уровне." : "Выбери, кого поставить на смену.";
            picker.Show();
        }

        /// <summary>«Рекомендация»: игра сама ставит тех, кто поможет в уровне (потом — общих помощников).</summary>
        public void Recommend()
        {
            var rec = Team.Recommend(_st);
            if (rec.Count == 0) { AudioService.Play("sfx_nope"); return; }
            AudioService.Play("sfx_button");
            _chosen.Clear();
            foreach (var id in rec) if (_chosen.Count < Team.Slots) _chosen.Add(id);
            Refresh();
            for (int i = 0; i < _chosen.Count && i < slots.Length; i++) Vfx.At("vfx_staff_pick", slots[i].transform, 300f, delay: 0.08f * i);
            infoText.text = string.Join(" · ", _chosen.Select(id => { var m = Team.Get(id); return $"{m.Info.Name}: {Team.EffectHere(m, _st)}"; }));
            Picked?.Invoke(_chosen.FirstOrDefault());
        }

        void Tap(StaffCardView c, bool fromPicker)
        {
            var m = Team.Get(c.id);
            if (m == null) return;
            if (m.Level <= 0)
            {
                AudioService.Play("sfx_nope");
                Tween.Shake((RectTransform)c.transform, 8f, 0.25f);
                string why = $"{m.Info.Name}: {m.Info.Skill[0]}. Придёт, когда {Comes(m.Info)}.";
                if (fromPicker && picker != null) picker.hintText.text = why; else infoText.text = why;
                return;
            }
            AudioService.Play("sfx_button");
            if (_chosen.Contains(c.id)) _chosen.Remove(c.id);
            else if (_chosen.Count < Team.Slots) _chosen.Add(c.id);
            else
            {
                // места заняты — меняем того, на чьё место нажали «+» (или последнего)
                int slot = _pickSlot >= 0 && _pickSlot < _chosen.Count ? _pickSlot : _chosen.Count - 1;
                _chosen[slot] = c.id;
            }
            _pickSlot = -1;
            if (fromPicker && picker != null) picker.Hide();
            if (_chosen.Contains(c.id))
            {
                int at = _chosen.IndexOf(c.id);
                if (at < slots.Length) Vfx.At("vfx_staff_pick", slots[at].transform, 300f);
                infoText.text = $"{m.Info.Name} (ур. {m.Level}): {Team.EffectHere(m, _st)}";
            }
            Refresh();
            Picked?.Invoke(c.id);
        }

        static string Comes(StaffInfo st)
        {
            if (st.Gold && !GameApp.I.Save.goldPath) return "откроешь Золотой путь";
            int room = House.RoomOf(st.Id);
            if (room < 0) return "построишь его комнату";
            var state = House.State(room);
            if (state == RoomState.Locked) return $"пройдёшь уровень {House.All[room].UnlockLevel} и построишь комнату «{House.All[room].Name}»";
            return state == RoomState.Ready ? $"заселишь его в «{House.All[room].Name}»" : $"построишь комнату «{House.All[room].Name}» в Торговом доме";
        }

        void Go()
        {
            AudioService.Play("sfx_button");
            if (picker != null) picker.HideInstant();
            var list = _chosen.ToList();
            Hide(() => _onStart?.Invoke(list));
        }

        /// <summary>Карточка сотрудника в окне «Команда».</summary>
        public StaffCardView Card(string id) => cards.FirstOrDefault(c => c != null && c.id == id);

        /// <summary>Программный выбор — для бота и проверок (без окна «Команда»).</summary>
        public void DebugPick(string id) { var c = Card(id); if (c != null) Tap(c, fromPicker: picker != null && picker.IsOpen); }
    }
}

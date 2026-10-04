using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Окно этажа (v3.2, 03.10.2026): три ячейки-комнаты. Закрытая — под замком «Уровень N», жмёшь на
    /// открытую — попадаешь внутрь (экран комнаты).
    /// </summary>
    public class FloorPopup : Popup
    {
        public TextMeshProUGUI title;
        public RoomCell[] cells = new RoomCell[3];
        public Button closeButton;

        HubController _hub;
        int _floor;
        float _tick;

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
            foreach (var c in cells)
            {
                var cell = c;
                cell.button.onClick.AddListener(() => Enter(cell));
            }
        }

        public int Floor => _floor;

        public void Open(HubController hub, int floor)
        {
            _hub = hub;
            _floor = floor;
            Refresh();
            Show();
            if (hub.tutorial != null) Tween.Delay(this, 0.5f, () => hub.tutorial.RunOnFloor(this));
        }

        void Update()
        {
            if (!IsOpen) return;
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            Refresh();
        }

        public void Refresh()
        {
            title.text = $"Этаж {_floor} · {House.FloorNames[_floor - 1]}";
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                int room = (_floor - 1) * 3 + i;
                c.room = room;
                var info = House.All[room];
                var st = House.State(room);
                c.nameText.text = info.Name;
                c.view.Show(room, House.IsBuilt(room) ? House.DecorCount(room) : 0);
                string icon = null, text = "";
                float shade = 0f;
                c.lockOverlay.SetActive(st == RoomState.Locked);
                c.lockText.text = "Ур. " + info.UnlockLevel;
                bool full = st == RoomState.Built && House.DecorCount(room) >= House.Decor;
                c.frame.sprite = full ? c.frameGold : c.frameNormal;
                switch (st)
                {
                    case RoomState.Locked:
                        shade = 0.45f; break;
                    case RoomState.Available:
                        icon = "icon_build"; text = House.BusyRoom >= 0 ? "Ждёт стройки" : "Построить"; shade = 0.4f; break;
                    case RoomState.Building:
                        icon = "icon_speed_up"; text = House.Time(House.BuildLeft(room)); shade = 0.4f; break;
                    case RoomState.Ready:
                        icon = "icon_move_in"; text = "Заселить!"; shade = 0.25f; break;
                }
                c.shade.gameObject.SetActive(shade > 0f);
                c.shade.color = new Color(0.08f, 0.06f, 0.05f, shade);
                c.stateIcon.gameObject.SetActive(icon != null);
                if (icon != null) c.stateIcon.sprite = ArtLibrary.S(icon);
                c.stateText.text = text;
                c.stateText.gameObject.SetActive(text.Length > 0);

                var staff = House.StaffOf(room);
                bool lives = st == RoomState.Built && staff != null && House.LevelOf(staff.Id) > 0;
                c.face.gameObject.SetActive(lives);
                if (lives) { c.face.sprite = StaffFace(staff.Id); c.face.preserveAspect = true; }
                int lvl = lives ? House.Level(room) : 0;
                for (int k = 0; k < c.stars.Length; k++)
                {
                    c.stars[k].gameObject.SetActive(lives);
                    c.stars[k].sprite = ArtLibrary.S(k < lvl ? "ui_star_big" : "ui_star_big_empty");
                }
            }
        }

        void Enter(RoomCell cell)
        {
            // закрытая комната тоже открывается — под цепями: видно, кто придёт, что умеет и после какого уровня
            AudioService.Play(House.State(cell.room) == RoomState.Locked ? "sfx_nope" : "sfx_button");
            Hide(() => _hub.roomScreen.OpenRoom(cell.room));
        }

        /// <summary>Портрет сотрудника; пока картинки нет — аватарка зверька из рейтинга или значок.</summary>
        public static Sprite StaffFace(string id) =>
            ArtLibrary.S($"staff_{id}_face") ?? ArtLibrary.S("av_" + (id == "rabbit" ? "bunny" : id)) ?? ArtLibrary.S("icon_staff_badge");

        public RoomCell Cell(int room) => cells[room % 3];
    }
}

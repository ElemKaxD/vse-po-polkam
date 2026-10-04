# Архитектура кода

- **Core** (`Scripts/Core`, asmdef без Unity): `ItemCatalog` (28 товаров), `LevelData` (DTO json), `LevelState`
  (состояние, Clone/Hash), `Rules` (ходы, авто-цель, тики, покупатели, грузчик +2 места), `Solver` (DFS),
  `LevelGenerator`, `LevelPlanner` (районы, механики, сложность), `Economy`, `RushRules`.
- **Game**: `GameController` (ввод, синхронизация вью по uid, события, FX, обучение, undo/hint, победа/поражение,
  Час пик, `Layout()` — раскладка поля). Вью: `ItemView`, `BeltView`, `SectionView` (морозилка — голубое стекло),
  `CartView` (всегда brd_cart_5, замки на лишних ячейках, тонировка), `GoalPanelView`, `CustomerView`, `GameHud`,
  `TutorialOverlay` (рука следует за целью, облачко уходит с цели, не ловит клики),
  окна `Victory/Defeat/Pause/Mechanic/RushResult`. Отладка: `DebugAutoMove/ToCart/RandomMove/Undo/UnlockInput`.
- **Hub**: `HubController.Show(screen)` (меню только на карте), `MapScreen` (страницы-районы, `ShowPage`),
  `MapNode`, `RenovationScreen`: районы 1–4 — ремонт-«лего» (`RenoLegoData`: голый магазин `legoBase`
  плюс слои `legoPieces`, купленный прилетает на своё место), районы 5+ — прежняя сцена `RenoStore`
  (здание worn→new после 1-го этапа; навесные предметы на всех этапах, наземные — только своего района;
  «картинки-магазины» только на карточке → `Celebrate()`). Карточки — полосой внизу (`LayoutCards`).
  `AlbumScreen` (три слоя: стол, ровная книга, ленты-закладки слева), `ShopScreen`, `DailyScreen`, `LeaderboardScreen`,
  попапы Settings/Pack/Offer/Confirm/`StarTrackPopup` («Звёздный путь», узлы `StarTrackNode`)/`WardrobePopup`
  («Гардероб»: ореол, скин енота, оформление магазина — `MetaCatalog.Cosmetics`, сейв `wearHalo/wearRaccoon/wearScene/wearCart`).
  `ShopScreen` — на нарисованном прилавке `bg_shop`, карточки без своих плашек.
  `HubTutorial` — шаги на карте и внутри ремонта и альбома. Окна ждут шага обучения (`HasMapStep`),
  обучение ждёт закрытия окон (`AnyPopupOpen`).
- **Торговый дом (03.10.2026)**: `House` (12 комнат, 10 сотрудников, стройка по таймеру — одна за раз, видео −30 мин,
  молоток; декор 6 предметов, каждые 2 — уровень жильца 1..4; касса копит монеты), `Team` (смена в уровне: 1 место,
  +1 за весь 2-й этаж, +1 за Буфет; умения меняют `LevelState` на старте — `FrozenTimer`, `SaleCoins`, замки,
  терпение, скоропорт, тележка; Ёж спасает от переполнения в `GameController.TryStaffRescue`; Белка/Кот — процент
  к монетам через `GameApp.ShiftCoinPercent`). Экраны: `HouseScreen` (дом из двух кадров, полосы этажей под
  `RectMask2D`, окна-состояния, кран), `FloorPopup`/`RoomCell` (рамка ui_room_cell, цепи на закрытой),
  `RoomScreen`/`DecorTile`, `RoomView` (основа + куски; `StaffX` — где поставить жильца).
  Смена перед уровнем — `Game/ShiftPopup` + `StaffCardView`: игрок выбирает сам, `Team.Recommend` — бирка «Советую»,
  `Team.Features` — ряд «Что ждёт в уровне». `GameController.AskShift` открывает окно после карточки механики,
  `ApplyShift` пересоздаёт `LevelState` с умениями. Бот (`BotBridge`) жмёт «Начать» с советованными (`DebugStartShift`).
  Обучение «в фокусе» — `TutorialOverlay.Focus(text, target, onTap, strict, light)`: цель получает свой Canvas без
  рейкастера (нажатие ловит tapCatcher, `TapPoint` помнит точку — strict пропускает только нажатие по цели).
- **Services**: `GameApp` (прогресс, кошелёк, ремонт, альбом, ежедневки, Час пик, переходы сцен), `Platform` (YG2),
  `MetaCatalog` (магазины, 89 предметов ремонта, наклейки, товары за рубли — цены и тексты тут), `SaveData`,
  `ArtLibrary`, `AudioService`, `BootController`.
- **UI**: `Popup`, `Toast`, `UiButton`, `AdaptiveScaler`, `ScaleToParent` (слой ценников масштабируется с картинкой).
- **Раскладка поля**: `BoardLayout` (Resources/BoardLayout.asset, одна/две ленты) — `GameController.Layout()` берёт
  положения лент/стеллажа/тележки оттуда; ассет не перезаписывается сборкой.
- **Editor**: `ScenePreviewWindow` (просмотр без Play), `UserLayout` (ручные правки переживают сборку),
  `SceneBuilder(.Hub)` строит все объекты сцен; `UiBuild` — хелперы; `RenoLayoutData` (генерируется);
  `MapRoadData` (дороги сегментов); `SceneValidator`; `BuildTools`; `ProjectSetup`; `ArtImportPostprocessor`.

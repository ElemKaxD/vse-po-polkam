# Инструменты

## Мост Unity MCP (свой клиент — MCP-инструменты в сессию не грузятся)
- `Tools/unity_cmd.py` — `send(type, params, timeout)`; порт из `~/.unity-mcp/unity-mcp-status-*.json`
  (project_name "Yandex_Claude"; порт бывает 6400/6401).
- Команды: `execute_menu_item`, `refresh_unity`, `read_console`, `manage_editor` (play/stop), `execute_code`.

## Скрипты
| Скрипт | Что делает |
|---|---|
| `Tools/rebuild.py [--setup] [--force]` | стоп Play → компиляция → «Служебное/Собрать сцены без подтверждения» → «Проверить сцены». Не пересобирает, если сцены правили руками после прошлой сборки |
| `Tools/ui_tour.py [hub map reno popups game]` | Play mode, сейв «середина игры», скриншоты экранов/окон/уровней в `$SHOTS_DIR`; `TOUR_LEVELS=3,46` |
| `Tools/grid.py out.jpg a b c d` | склейка 4 скриншотов 2×2 (экономия токенов) |
| `Tools/editor_shot.py out.jpg [w]` | скрин открытой сцены **без Play**: канвы временно в ScreenSpaceCamera → RT 1920×1080 |
| `Tools/ArtPipeline/ui_kit_sheets.py <dir>` | листы-киты всей графики с подписями имён (для промптов референсов) |
| `Tools/unity_wait.py 300 --refresh` | ждать компиляцию, печатать ошибки |
| `Tools/unity_play.py click/shot/eval` | ручной автотест в Play |
| `Tools/autoplay.py <уровни>` | прогон уровней солвером |
| `Tools/ArtPipeline/normalize_art.py` | нормализация PNG (стороны кратны 4 — иначе нет сжатия) |
| `Tools/ArtPipeline/reno_preview.py <dir>` / `--cs` | превью раскладки ремонта / генерация `Editor/RenoLayoutData.cs` из `reno_layout.json` |
| `Tools/LevelTool` (`dotnet run -c Release -- 1 200 60`) | генерация 200 уровней |

## Просмотр без Play (ScenePreviewWindow)
Вызов из execute_code через рефлексию (методы приватные):
`var w = EditorWindow.GetWindow<ScenePreviewWindow>(); t.GetField("_level", bf).SetValue(w, 46); t.GetMethod("ShowLevel", bf).Invoke(w, null);`
Методы: ShowLevel (_level, _moves), ShowTutorial, ShowRushHud, GamePopup(Func<GC,Popup>, Action<GC>), HubScreen(Func<Hub,HubScreen>),
ShowMap(d), ShowReno (_renoDistrict, _renovated, _allItems), HubPopup. После — `w.Close()` (иначе окно висит поверх Game).
`GameController.EditorPreview(data, moves)` — товары с DontSave; Tween в Edit mode сразу доводит до конца (Punch/Shake — пропуск).

## Окна редактора для пользователя
- «Всё по полкам → Редактор интерфейса» (`UiEditorWindow`, 29.09.2026) — правка UI **в запущенной игре**:
  кнопки открывают любой экран хаба, карту/ремонт любого района (ремонт: ничего/половина/всё куплено),
  окна хаба, награды («Звёздный путь» с готовой наградой, пачка наклеек, победа ★/★★/★★★ и после ×2),
  уровень 1–110, +5 ходов бота, поражение/пауза/механика/итоги «Часа пик»/облачко обучения,
  размер экрана 16:9 / 21:9 / 16:10 / 4:3, сейв «середина игры». Запоминаются только ВЫДЕЛЕННЫЕ объекты:
  при выделении — «как было» (если объект в этот момент едет анимацией Tween по позиции — после её конца),
  «Сохранить правки» пишет разницу через `UserLayout.Record` в user_layout.json; выход из игры сохраняет
  сам и на EnteredEditMode переносит правки в файлы сцен (`UserLayout.ApplyToSceneFiles`).
  Масштаб окон (анимация появления) и «дышащих» кнопок не записывается — берётся «в покое»
  (`UserLayout.RestScale`). Объекты, которые двигает код (`UserLayout.IsDriven`), не сохраняются — окно
  предупреждает. Проверка из скрипта: методы приватные, вызывать рефлексией (как ScenePreviewWindow).
- «Всё по полкам → Просмотр сцен и уровней» (`ScenePreviewWindow`) — показать любой экран/уровень/окно без Play.
- «Всё по полкам → Иконки и графика» (`ArtBrowserWindow`) — вся графика с именами и размерами, поиск,
  фильтр по папке, клик копирует имя в буфер, двойной клик ставит спрайт выделенному объекту,
  кнопка «Подогнать размер под пропорции», кнопка «Обновить» пересобирает ArtLibrary.
- Оба окна открываются вкладкой рядом с Inspector — если пользователь «не видит окно», оно за соседней вкладкой.

## Проверка сцен
`Проверить сцены` пишет полный список проблем в `Temp/validate.txt` — мост отдаёт в консоль только первую строку.
Проверяет: незаполненные ссылки, растянутые картинки и надписи, которые не влезают в свою рамку
(для автоподбора кегля меряет по `fontSizeMin`), и прозрачные PNG, импортированные без альфы
(«Alpha Source = None» → прозрачное в игре чёрное; так 28.09 почернели углы таблички полки `brd_tag_base`).

## Ручные правки пользователя
`UserLayout`: после сборки снимок «как построено» → `UserSettings/AllOnShelves_built_*.json`; перед сборкой diff →
`Editor/Data/user_layout.json` (путь в иерархии, `Имя#k` для одинаковых имён) → применяется в `SceneBuilder.Save()`.
Не отслеживаются объекты, которые двигает код (поле уровня, товары, страницы карты, аватар, панели попапов, дети LayoutGroup),
и объекты, чей размер считает компонент (`AspectRatioFitter` фонов, `ScaleToParent`): иначе после проверки экрана 4:3
размеры фонов «запоминались» как правки (29.09.2026; такие записи сборка теперь сама удаляет из файла).
Правка точечного объекта во весь экран перепривязывается к ближайшему краю (`UserLayout.NearestEdge`: левая треть
макета — к левому краю и т. д.) — иначе кнопки, перетащенные к другому краю, уезжали на экранах не 16:9.

## Ловушки
- Правка скриптов во время Play mode: Unity перекомпилирует прямо в игре, статические поля обнуляются
  (`GameApp.I == null`, окна не открываются). Сначала выйти из Play, потом править код.
- Проверка пропорций экрана: `python Tools/aspect_check.py [WxH ...]` — карта, ремонт, уровень на
  1920x1080, 2560x1080, 1920x1200, 1440x1080, 1280x1024. После неё вернуть окно Game в 16:9.
- Экономика: `python Tools/economy_sim.py [--runs 10] [-v] [--stage 0,300,...] [--set Имя=число]` — 5 типов игроков
  день за днём, цены из Economy.cs / MetaCatalog.cs, уровни из levels.json; без Unity. Подробно — economy.md.
- Меню «2. Собрать сцены» спрашивает подтверждение (защита ручных правок) — автоматика вызывает только
  «Служебное/Собрать сцены без подтверждения».
- **Если сборка падает с `ExecutionEngineException: String conversion error: Illegal byte sequence`** —
  помогает только перезапуск редактора (RequestScriptReload и refresh не спасают):
  `Stop-Process -Id <PID Unity.exe>` (PID — по `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"`,
  брать тот, где `-projectpath ...Yandex_Claude`), затем `Start-Process Unity.exe -ArgumentList '-projectPath',...`.
  После принудительного закрытия Unity показывает окно «Recovering Scene Backups» — отвечать **No**
  (сцены уже сохранены). Окно кликается через user32 `SetCursorPos`+`mouse_event`, скрин экрана —
  `System.Windows.Forms.Screen` + `CopyFromScreen`.
- Загрузочный экран шаблона YG2 ждёт `Assets/WebGLTemplates/YandexGames/Images/{logo,background}.png` —
  без них в консоли 404 и пустая заставка.
- `execute_code` грузит динамические сборки → WebGL-сборка падает в InputSystem LinkFileGenerator
  («Illegal byte sequence»). Перед сборкой: `refresh_unity` (compile=request, mode=force) без execute_code,
  подождать `reloading:false` в status-файле, затем `execute_menu_item`
  «Всё по полкам/3. Собрать WebGL для Яндекс Игр» (вызов синхронный, таймаут клиента — нормально),
  ждать `Temp/build_result.txt`. **Ни один инструмент по дороге не должен звать execute_code** —
  `unity_wait.py` раньше делал это в цикле ожидания и портил сборку (исправлено 26.09.2026,
  состояние читается из `~/.unity-mcp/unity-mcp-status-*.json`; `--code` возвращает старое поведение).
  Если домен уже «отравлен» — помогает только перезапуск редактора.
- Код внутри `#if UNITY_EDITOR`, вызываемый обычными методами, компилируется в редакторе и роняет
  сборку плеера (`CS0103`). Проверять перед билдом: сборка сцен и «Проверить сцены» этого не ловят.
- `Editor.log` общий для всех запущенных Unity: если открыт другой проект (yandex_games), лог — его.
  Консоль своего редактора — только через `read_console`.
- Тест WebGL в браузере: копировать `Builds/WebGL` в scratchpad и раздавать оттуда (сервер в папке сборки
  блокирует её удаление при следующей сборке). Сжатие Brotli + decompressionFallback — работает на любом сервере.
- Скрипты с кириллицей в пути: `subprocess.run([...])`, не `os.system`. Длинные правки C# — через .py-файл
  (heredoc bash ломается на кавычках).

## Перенос сгенерированной графики
Пользователь кладёт новые PNG в `D:\Work\ЯндексИгры\LaserSiege\yandex_games\Assets\AllOnShelves\Art\<папка>`
(это отдельная папка-источник, не проект игры). Перенос:
`python Tools/ArtPipeline/normalize_art.py --newer 12` — только файлы свежее N часов (без ключа — всё подряд,
это переимпорт всей графики и долгий reimport в Unity). Потом меню «1. Подготовить проект» (9-slice считается
от новых размеров) и `Tools/rebuild.py`.

## Картинки в сцену (UiDropTool)
Перетаскивание PNG из Project прямо в окно Scene раньше делало SpriteRenderer — под Overlay-канвой его не видно.
`Editor/UiDropTool.cs` перехватывает drop и создаёт UI Image в канве (родные пропорции, точка броска,
родитель — выделенный объект → открытый экран → корень канвы). Меню «Починить невидимые картинки» чинит
уже брошенные объекты. В окне «Иконки и графика» есть кнопки «Добавить на экран» и «Починить невидимые».

- `Tools/ArtPipeline/map_nodes.py` — ставит ценники уровней по дороге на картинке района → `Editor/MapRoadData.cs`.
- `Tools/ArtPipeline/gen_bars.py` — рисует полосу, заливку и ручку ползунка; `gen_halos.py` — временные ореолы.
- `Tools/ArtPipeline/lego_extract.py` — нарезает улучшения ремонта из серии картинок «убрали по одному».
- `Tools/ArtPipeline/lego_build.py` — из серий `meta_storeN_stepNN` делает голый магазин + слои района
  и пишет `Scripts/Services/RenoLegoData.cs`. Запускать после присылки новой серии.
- `Tools/ArtPipeline/room_build.py [комнаты] [--preview папка]` — комнаты Торгового дома (03.10.2026): из серий
  `Team/Rooms/room_NN_step01..07` делает голую комнату `room_NN_base` (кадр 1672×940 как есть) и куски декора
  `room_NN_iK_P` + `Scripts/Services/RoomLegoData.cs`. Серии генератора не «чистое стирание» (вещи возвращаются,
  кадр перерисован), поэтому пиксель относится к предмету, после снятия которого он больше ни разу не похож на
  полный кадр; содержимое — из step01 (все куски поверх основы = step01). Пустой слой (кадр сделан из полного, а не
  из предыдущего) забирает половину кусков у соседа. Предмет режется на куски по скоплениям — без пустых рамок
  на полкадра (вес вдвое меньше). `--preview` — картинка всех 7 состояний комнаты для проверки глазами.
  Он же кладёт дом (`house_open`, `house_scaffold` — без обрезки, совпадают пиксель в пиксель), `team_bg` и
  `room_build_overlay`. Остальное из `Art/Team` переносит `normalize_art.py`.

## Витрина Яндекса (28.09.2026)
- `python Tools/promo_capture.py shots` — скриншоты 1920×1080 в `Builds/Promo/` (уровни `PROMO_SHOTS`, ходов `PROMO_MOVES`,
  карта, ремонт в процессе). `video` — запись геймплея ботом (`Scripts/Game/PromoRecorder.cs`, только редактор) + ffmpeg;
  пока не нужен (пользователь отказался от видео).
- Иконка 512 и обложка 800×470 собраны из спрайтов игры: `Builds/Promo/icon_512.png`, `cover_800x470.png`.
- Всё для загрузки в консоль — `Builds/Yandex_Upload/` (архив, иконка, обложка, скриншоты по порядку).
- Консоль не даёт подложить файлы скриптом: CSP закрывает fetch/iframe к localhost, окно выбора файла из автоматики
  не открывается. Файлы выбирает пользователь, поля заполняются через `javascript_tool` (нативный setter + input).

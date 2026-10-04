# Яндекс Игры (YG2)
- **Платформа выбрана только 30.09.2026.** До этого `SettingsYG2 → Basic.platform` был пуст (файл
  `Platforms/YandexGames/YandexGames.asset` ссылался на скрипт с несуществующим GUID `5d228dc2…` — несовпадение
  ядра 2.0092 и модуля платформы 1.0091), символа `YandexGamesPlatform_yg` не было, и весь код интеграции
  с Яндексом (реклама, покупки, облачные сейвы, 48 файлов) в сборку не попадал. Исправлено: ссылка на
  `PlatformSettings` (`3b719eea…`), платформа выбрана, `autoApplySettings = false` (иначе плагин перед
  сборкой перезаписывает наши настройки плеера: сжатие, урезание кода, runInBackground).
- **Скрипты редактора должны компилироваться под WebGL**, иначе плагин пропускает доработку `index.html`
  (`#if PLATFORM_WEBGL`: код модулей, заставка). Было: активный профиль сборки «Windows»
  (`Assets/Settings/Build Profiles/Windows.asset`). Сейчас активна классическая платформа WebGL;
  `BuildTools` сам переключает и останавливает сборку («Build Aborted: … собери ещё раз»), если это слетит.
  Признак правильной сборки — в `Builds/WebGL/index.html` строка `[PluginYG2 v2.0092] [Build: N] [Platform: YandexGames]`.
- Локально (без SDK) в консоли «SDK is not initialized», «Failed - Game Ready» — это нормально, игра идёт.
- Сейв: partial `YG.SavesYG` с полем `aos` (`SaveData`).
- Реклама: `Platform.TryInterstitial` (с уровня 4, ≥60 c между, ≥45 c после rewarded, не в первую минуту),
  `Platform.ShowRewarded(id, ok, fail)`; sticky-баннер в хабе.
- Покупки: `YG2.purchases` — каталог; `consumed == false` — куплено и не обработано; `ConsumePurchaseByID(id, false)`.
  ID: starter_pack, no_ads, coins_s/m/l/xl, helpers_pack, undo_24h, theme_night/winter/farm, set_gold (30.09.2026).
  Наборы (`theme_*`, `set_gold`, стартовый) выдают скины из `ProductInfo.Cosmetics` — см. economy.md.
  Имитация покупок в редакторе — `SettingsYG2 → Payments`, заполняется из каталога «Подготовить проект»
  (`ProjectSetup.ConfigureYG`); после правки цен/товаров — перезапустить.
- Лидерборды: levels, stars, rush. В редакторе плагин отдаёт заглушку «no data» — это нормально.
- **Рейтинг 01.10.2026** (`Scripts/Services/League.cs`, экран `LeaderboardScreen`): ещё таблицы `day` и `week`.
  Сброса по времени у Яндекса нет — номер периода «вшит» в очки: day = номер_дня × 100 000 + очки,
  week = номер_недели × 1 000 000 + очки (время московское у всех). Экран показывает только записи текущего
  периода. Очки: новый уровень 10 + 5/★ (+20 финал района), переигровка 5 + 5/★, «Заказ дня» 30, «Час пик» очки/50.
  Итог периода — последнее место, которое видела игра (`League.OnData` ловит любой ответ таблицы; при входе
  в хаб `League.RefreshRanks`). Сменился день/неделя → `Save.lbPend*` → на карте окно «Итоги недели: 7-е место!»
  → «Забрать» (`LeaderboardScreen.ClaimWithPopup`). Награды — `League.DayReward/WeekReward`, рамки аватарки
  `frame_*` только отсюда. В extraData записи — `a:зверёк|f:рамка|n:ник` (аватарка из «Гардероба», вкладка «Аватар»).
  Запросы к таблицам идут через очередь `Platform.Tick` (не чаще раза в 1,1 с — ограничение Яндекса).
  В редакторе экран рисует выдуманную таблицу (`DemoPlayers`, только `#if UNITY_EDITOR`).
- Метрика: level_start/win/lose, ad_*, iap_purchase, meta_buy, album_pack, rush_end.
- `autoGRA=false`: GameReady вызывается вручную на первом интерактивном экране.
- Плагин сам архивирует сборку в `Builds/WebGL_*_Build(N).zip` — `BuildTools` удаляет эти копии.

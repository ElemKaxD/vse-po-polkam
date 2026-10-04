# «Всё по полкам!» — играть из облака Claude

Это **полная готовая игра**, не только HTML-обёртка. Unity на облачной машине не нужен.
`game.zip` содержит index.html, Build/ (движок, WASM, ресурсы), Images/, StreamingAssets/ и локальный HTTP-сервер.
`manifest.json` содержит размеры и SHA-256 всех файлов. Архив включён в Git, не является Git LFS-ссылкой.

Проверено 04.10.2026 в Chromium/WebGL 2: уровень 1 пройден обычными кликами,
после перезагрузки восстановлены 37 монет, 3 звезды и доступ к уровню 2.
Новый запуск — без ошибок в консоли. Доказательство: `verified-progress.png`.
Это локальная проверка готового билда; запуск в конкретной облачной среде Claude
нужно подтвердить отдельно, поскольку её браузер и ограничения нам недоступны.

## Запуск в Linux-контейнере

В корне checkout этой ветки:

```bash
python3 CloudPreview/cloud_preview.py check
PREVIEW_DIR="$(python3 CloudPreview/cloud_preview.py unpack)"
python3 "$PREVIEW_DIR/run_webgl.py" --no-browser --port 8123
```

Сервер оставить работающим. Открыть `http://127.0.0.1:8123/index.html` в Chromium **в той же облачной среде**.
Если порт занят, выбрать другой явно и использовать его в URL. `file://` не подходит.
Не нужны localhost пользователя, Unity Editor, MCP, Windows и доступ к его компьютеру.

## Управление через Playwright

Если среда уже предоставляет браузер, использовать его инструменты. Иначе (при наличии права установки):

```bash
cd CloudPreview
npm install --no-save playwright@1.63.0
npx playwright install --with-deps chromium
```

Запустить приведённый JavaScript в Node REPL/своём скрипте из CloudPreview:

```javascript
const {chromium} = require('playwright');
const fs = require('node:fs');
fs.mkdirSync('shots', {recursive: true});
const context = await chromium.launchPersistentContext('playwright-profile', {
  headless: true,
  viewport: {width: 1280, height: 720},
  args: ['--use-gl=angle', '--use-angle=swiftshader'],
});
const page = await context.newPage();
page.on('pageerror', e => console.error('PAGE ERROR:', e.message));
page.on('console', m => {
  if (m.type() === 'error') console.error('BROWSER ERROR:', m.text());
});
// Подписаться ДО перехода, чтобы не пропустить подтверждение от Unity.
const ready = page.waitForEvent('console', {
  predicate: m => m.text().includes('Init Game Success'), timeout: 180000,
});
await page.goto('http://127.0.0.1:8123/index.html', {waitUntil: 'domcontentloaded'});
await ready;
await page.screenshot({path: 'shots/01-start.png'});
// ПРОЧИТАТЬ screenshot, выбрать товар/кнопку и кликнуть по его координатам:
// await page.mouse.click(x, y);
// После каждого хода сделать и прочитать новый screenshot.
// Перетаскивание: move → down → move({steps: 12}) → up.
// Не закрывать context между ходами; profile хранит прогресс между сессиями.
```

Кнопки и товары нарисованы внутри Unity canvas: поиск по DOM-тексту не найдёт их.
Играть по скриншотам, координатами мыши; не подменять сохранения и не вызывать внутренние методы игры.
Для Unity-рендера нужен WebGL 2. Если контекст недоступен — сообщить это, а не утверждать, что игра запустилась.
Не добавлять `--disable-webgl` или `--disable-software-rasterizer`.
Если Chromium требует `--enable-unsafe-swiftshader`, использовать этот флаг только после разрешения среды,
в изолированном тестовом браузере для доверенной локальной игры; не менять настройки обычного браузера.

## Что проверить

1. Загрузка закончилась, видно поле уровня и товары (не чёрный canvas/загрузчик).
2. Пройти обучение и уровни 1–3 кликами/перетаскиванием, сохранять доказательства экрана после действий.
3. Выйти на карту, открыть доступные окна, проверить реакции кнопок и отсутствие ошибок.
4. После сохранения перезагрузить страницу в том же профиле и на том же порту; проверить прогресс.
5. Отдельно указать ограничения: настоящая реклама, платежи, лидерборды и Яндекс SDK не проверяются на localhost.

**Ограничение:** Claude без терминала/выполнения кода и браузера не сможет играть: чтение файлов и картинок
не выполняет WebGL. Такой чат должен передать задачу в облачную среду с браузером; этот пакет обеспечивает файлы,
но не добавляет отсутствующие инструменты самому Claude.

Документация: [браузеры Playwright](https://playwright.dev/docs/browsers),
[программный рендер Chromium](https://github.com/chromium/chromium/blob/main/docs/gpu/swiftshader.md).

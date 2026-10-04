// Результаты BotBridge уходят в страницу: window.botResult(json) — их читает BuildBot.
// Имя функции обязано совпадать с DllImport("SendResult") — иначе wasm-ld не найдёт символ.
mergeInto(LibraryManager.library, {
    SendResult: function (jsonPtr) {
        var s = UTF8ToString(jsonPtr);
        if (window.botResult) window.botResult(s);
    },
});

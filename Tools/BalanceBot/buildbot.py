# -*- coding: utf-8 -*-
"""BuildBot — бот, играющий в БИЛД игры (WebGL) через браузер.

Как работает: копирует билд, дописывает в index.html JS-мост, раздаёт копию локальным
сервером, открывает браузер, общается со страницей по WebSocket и играет уровнями
через BotBridge в игре. Профили: optimal (хардкорщик), casual (сильный игрок),
granny (бабушка). Строит отчёт-инфографику по всем профилям.

Запуск: python buildbot.py            (или BuildBot.exe — то же меню)
Требует: собранный WebGL-билд (меню Unity «3. Собрать WebGL»).
"""
import base64
import glob
import http.server
import io
import json
import os
import socketserver
import socket
import threading
import time
import webbrowser

import matplotlib
import matplotlib.pyplot as plt

# ------------------------------ WebSocket-сервер (без зависимостей) ------------------------------

WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"


def ws_handshake(conn):
    data = b""
    while b"\r\n\r\n" not in data:
        chunk = conn.recv(4096)
        if not chunk:
            return False
        data += chunk
    import hashlib
    key = ""
    for line in data.decode("latin1").split("\r\n"):
        if line.lower().startswith("sec-websocket-key:"):
            key = line.split(":", 1)[1].strip()
    accept = base64.b64encode(hashlib.sha1((key + WS_GUID).encode()).digest()).decode()
    conn.sendall(("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\n"
                  "Connection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n").encode())
    return True


def ws_send(conn, text):
    payload = text.encode("utf-8")
    mask = 0x81
    header = bytes([mask])
    n = len(payload)
    if n < 126:
        header += bytes([n])
    elif n < 65536:
        header += bytes([126]) + n.to_bytes(2, "big")
    else:
        header += bytes([127]) + n.to_bytes(8, "big")
    conn.sendall(header + payload)


def ws_recv(conn):
    def read(n):
        buf = b""
        while len(buf) < n:
            c = conn.recv(n - len(buf))
            if not c:
                raise IOError("ws closed")
            buf += c
        return buf
    hdr = read(2)
    length = hdr[1] & 0x7F
    if length == 126:
        length = int.from_bytes(read(2), "big")
    elif length == 127:
        length = int.from_bytes(read(8), "big")
    if hdr[1] & 0x80:
        read(4)  # маска клиентов не бывает от нас, но браузер шлёт маскированные
    return read(length).decode("utf-8", "replace")


def ws_recv_masked(conn):
    def read(n):
        buf = b""
        while len(buf) < n:
            c = conn.recv(n - len(buf))
            if not c:
                raise IOError("ws closed")
            buf += c
        return buf
    hdr = read(2)
    length = hdr[1] & 0x7F
    if length == 126:
        length = int.from_bytes(read(2), "big")
    elif length == 127:
        length = int.from_bytes(read(8), "big")
    masked = hdr[1] & 0x80
    mask = read(4) if masked else b"\x00\x00\x00\x00"
    data = read(length)
    if masked:
        data = bytes(b ^ mask[i % 4] for i, b in enumerate(data))
    return data.decode("utf-8", "replace")


# ------------------------------ страница-мост ------------------------------

BRIDGE_JS = """
<script>
(function(){
  var lastResult = null;
  var pending = [];
  var wsPort = new URLSearchParams(location.search).get('wsport') || '8771';
  window.botResult = function(s){ lastResult = s; };
  window.__botCall = function(json){
    try { ygGameInstance.SendMessage('BotBridge', 'Cmd', json); } catch(e){ return 'ERR:'+e; }
    return null;
  };
  window.__botPoll = function(){ var r = lastResult; lastResult = null; return r; };
  function connect(){
    var ws;
    try { ws = new WebSocket('ws://127.0.0.1:' + wsPort); } catch(e){ return; }
    ws.onopen = function(){ ws.send(JSON.stringify({hello:'page'})); };
    ws.onmessage = function(ev){
      var msg;
      try { msg = JSON.parse(ev.data); } catch(e){ return; }
      if (msg.cmd === 'call'){
        window.__botCall(msg.json);
        // результат придёт асинхронно в botResult — отправим когда появится
        var tries = 0;
        (function poll(){
          var r = window.__botPoll();
          if (r !== null && r !== undefined){ ws.send(JSON.stringify({reply: r})); return; }
          if (++tries < 2000) setTimeout(poll, 25);
          else ws.send(JSON.stringify({reply: '{"error":"timeout"}'}));
        })();
      }
    };
    ws.onclose = function(){ setTimeout(connect, 1000); };
  }
  connect();
})();
</script>
"""


def patch_index(build_dir):
    """Копия билда целиком + JS-мост в index.html. Возвращает путь к копии."""
    import shutil
    dst = os.path.join(data_dir(), "site")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    shutil.copytree(build_dir, dst)
    idx = os.path.join(dst, "index.html")
    html = open(idx, encoding="utf-8").read()
    if "__botCall" not in html:
        html = html.replace("</body>", BRIDGE_JS + "</body>")
        open(idx, "w", encoding="utf-8").write(html)
    return dst


# ------------------------------ HTTP-сервер ------------------------------

def serve_site(directory, port=8770):
    class H(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=directory, **kw)
        def log_message(self, *a):
            pass
    socketserver.TCPServer.allow_reuse_address = True
    httpd = socketserver.TCPServer(("127.0.0.1", port), H)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd


# ------------------------------ сам бот ------------------------------

class GameLink:
    """Связь со страницей игры через WebSocket. Ответы страницы складываются в очередь."""

    def __init__(self):
        self.conn = None
        self.queue = __import__("collections").deque()
        server = socket.socket()
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind(("127.0.0.1", 0))          # свободный порт: старые вкладки не подключатся
        self.port = server.getsockname()[1]
        server.listen(2)
        self.listener = server
        threading.Thread(target=self._accept, daemon=True).start()

    def _accept(self):
        while True:
            try:
                conn, _ = self.listener.accept()
            except OSError:
                return
            if not ws_handshake(conn):
                conn.close()
                continue
            if self.conn is not None:
                try:
                    self.conn.close()   # новая вкладка вытесняет старую — ответы не путаются
                except Exception:
                    pass
            self.queue.clear()
            self.conn = conn
            threading.Thread(target=self._pump, args=(conn,), daemon=True).start()

    def _pump(self, conn):
        """Единственный читатель сокета: ответы складываем в очередь (иначе гонка с call())."""
        try:
            while True:
                msg = ws_recv_masked(conn)
                try:
                    d = json.loads(msg)
                except Exception:
                    continue
                if "reply" in d:
                    self.queue.append(d["reply"])
        except Exception:
            if self.conn is conn:
                self.conn = None

    def wait_page(self, timeout=90):
        print("   жду страницу игры (откроется браузер)…")
        t0 = time.time()
        while time.time() - t0 < timeout:
            if self.conn is not None:
                r = self.call('{"cmd":"ping"}', timeout=20)
                if r and "ok" in r:
                    return True
            time.sleep(0.5)
        return False

    def call(self, json_cmd, timeout=90):
        if self.conn is None:
            raise IOError("страница не подключена")
        self.queue.clear()
        ws_send(self.conn, json.dumps({"cmd": "call", "json": json_cmd}))
        t0 = time.time()
        while time.time() - t0 < timeout:
            if self.queue:
                return self.queue.popleft()
            time.sleep(0.05)
        return None


def app_dir():
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


def data_dir():
    d = os.path.join(app_dir(), "data")
    os.makedirs(d, exist_ok=True)
    return d


def load_config():
    path = os.path.join(app_dir(), "config.json")
    default = {"build": r"D:\Work\ЯндексИгры\LaserSiege\Yandex_Claude\Builds\WebGL",
               "project": r"D:\Work\ЯндексИгры\LaserSiege\Yandex_Claude"}
    if os.path.exists(path):
        try:
            default.update(json.load(open(path, encoding="utf-8")))
        except Exception:
            pass
    else:
        json.dump(default, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return default


PROFILES = {
    "optimal": {"delay": 0.12, "label": "хардкорщик"},
    "casual":  {"delay": 0.25, "label": "сильный игрок"},
    "granny":  {"delay": 0.6,  "label": "бабушка"},
}


def run_levels(link, first, last, profiles, run_path):
    run = json.load(open(run_path, encoding="utf-8")) if os.path.exists(run_path) else {}
    for profile in profiles:
        cfg = PROFILES[profile]
        print(f"== профиль: {cfg['label']} ({profile})")
        for lvl in range(first, last + 1):
            t0 = time.time()
            link.call('{"cmd":"play","level":%d}' % lvl)
            # ждём загрузки уровня: сцена game и результат Playing (в браузере грузится небыстро)
            loaded = False
            for _ in range(50):
                time.sleep(0.5)
                r = link.call('{"cmd":"state"}', timeout=15)
                try:
                    if r and json.loads(r).get("scene") == "game" and json.loads(r).get("result") == "Playing":
                        loaded = True
                        break
                except Exception:
                    pass
            moves, stuck, result = 0, 0, ("error:not-loaded" if not loaded else "?")
            for i in range(500):
                r = link.call('{"cmd":"move","policy":"%s"}' % profile)
                if r is None:
                    result = "error:timeout"
                    break
                try:
                    d = json.loads(r)
                except Exception:
                    result = "error:parse"
                    break
                if d.get("moved"):
                    moves += 1
                else:
                    stuck += 1
                if d.get("result") in ("Win", "Lose"):
                    result = d["result"]
                    break
                time.sleep(cfg["delay"])
            run.setdefault(profile, {})[str(lvl)] = {
                "moves": moves, "result": result, "stuck": stuck,
                "sec": round(time.time() - t0, 1)}
            json.dump(run, open(run_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            print(f"   L{lvl}: {result}  ходов={moves}  {run[profile][str(lvl)]['sec']}c", flush=True)
        link.call('{"cmd":"hub"}')
        time.sleep(1.5)


# ------------------------------ отчёт ------------------------------

def build_report(project):
    run_path = os.path.join(data_dir(), "run_build.json")
    run = json.load(open(run_path, encoding="utf-8")) if os.path.exists(run_path) else {}
    levels = json.load(open(os.path.join(project, "Assets", "AllOnShelves", "Resources",
                                         "Levels", "levels.json"), encoding="utf-8"))["levels"]
    lm = {l["id"]: l for l in levels}
    plt.rcParams.update({"figure.dpi": 110, "font.size": 9, "axes.grid": True, "grid.alpha": 0.3})
    cards, warnings, conclusions = [], [], []
    colors = {"optimal": "#2a8", "casual": "#48c", "granny": "#d44"}

    def fig64(fig):
        buf = io.BytesIO()
        fig.savefig(buf, format="png", bbox_inches="tight")
        plt.close(fig)
        return base64.b64encode(buf.getvalue()).decode()

    for profile, prun in run.items():
        ids = sorted(int(k) for k in prun)
        if not ids:
            continue
        label = PROFILES.get(profile, {}).get("label", profile)
        wins = [i for i in ids if prun[str(i)]["result"] == "Win"]
        loses = [i for i in ids if prun[str(i)]["result"] == "Lose"]
        fig, ax = plt.subplots(figsize=(10, 3))
        xs = ids
        ys = [prun[str(i)]["moves"] for i in xs]
        opt = [lm[i]["metrics"]["moves"] for i in xs]
        ax.bar(xs, ys, color=colors.get(profile, "#888"), label="ходы бота")
        ax.plot(xs, opt, "k--", lw=1, label="оптимум")
        ax.set_xlabel("уровень"); ax.set_ylabel("ходы"); ax.legend(fontsize=8)
        ax.set_title(f"Профиль «{label}»: ходы против оптимума")
        body = (f"Побед: <b>{len(wins)}/{len(ids)}</b>."
                + (f" Поражений: {loses}." if loses else "")
                + f" Средний запас ходов: {sum(ys) / len(ys) - sum(opt) / len(opt):.1f}.")
        if profile == "granny" and loses:
            warnings.append(f"«Бабушка» проигрывает уровни {loses} — казуалу не хватает подсказок/запаса ходов.")
        if profile == "optimal" and loses:
            warnings.append(f"Даже «хардкорщик» проигрывает {loses} — уровень, возможно, нерешаем балансом.")
        cards.append((f"Профиль «{label}»", fig64(fig), body))

    html = """<!doctype html><html><head><meta charset='utf-8'><title>BuildBot — баланс по профилям</title>
<style>body{font-family:'Segoe UI',Arial,sans-serif;background:#171717;color:#eee;margin:24px}
h1{font-size:22px}.card{background:#222;border:1px solid #333;border-radius:10px;padding:14px;margin:14px 0}
.card h2{font-size:15px;margin:0 0 10px;color:#8fd}img{max-width:100%;border-radius:6px}li{margin:4px 0}</style>
</head><body><h1>🕹 BuildBot — баланс билда по профилям игроков</h1>
<p>""" + time.strftime("%d.%m.%Y %H:%M") + """</p>"""
    for title, img, body in cards:
        html += f"<div class='card'><h2>{title}</h2><img src='data:image/png;base64,{img}'/>{body}</div>"
    if warnings or conclusions:
        html += "<div class='card'><h2>Наблюдения</h2><ul>" \
                + "".join(f"<li>⚠ {w}</li>" for w in warnings) \
                + "".join(f"<li>✅ {c}</li>" for c in conclusions) + "</ul></div>"
    html += "</body></html>"
    path = os.path.join(data_dir(), "report_build.html")
    open(path, "w", encoding="utf-8").write(html)
    print("   отчёт ->", path)
    try:
        os.startfile(path)
    except Exception:
        pass


# ------------------------------ меню ------------------------------

import sys  # noqa: E402


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    cfg = load_config()
    print("""
==========================================================
   BUILDBOT — бот играет в БИЛД игры (браузер)
==========================================================
""")
    build = cfg["build"]
    if not os.path.exists(os.path.join(build, "index.html")):
        print("✖ Билд не найден:", build)
        print("  Сначала собери WebGL: Unity → меню «Всё по полкам → 3. Собрать WebGL».")
        input("Enter — выход…")
        return
    print("1 — полный прогон: все уровни, все 3 профиля (долго!)")
    print("2 — диапазон уровней (выбрать профили)")
    print("3 — только отчёт по собранному")
    print("0 — выход")
    choice = input("Выбор: ").strip()
    if choice in ("1", "2"):
        profiles = ["optimal", "casual", "granny"]
        first, last = 1, 110
        if choice == "2":
            first = int(input("от уровня [1]: ").strip() or 1)
            last = int(input("до уровня [10]: ").strip() or 10)
            ps = input("профили через запятую [optimal,casual,granny]: ").strip()
            if ps:
                profiles = [p.strip() for p in ps.split(",") if p.strip() in PROFILES]
        site = patch_index(build)
        print("   раздаю билд:", site)
        serve_site(site)
        link = GameLink()
        webbrowser.open(f"http://127.0.0.1:8770/index.html?wsport={link.port}")
        if not link.wait_page():
            print("✖ Страница не подключилась к боту (WebSocket). Открой http://127.0.0.1:8770 вручную.")
            input("Enter — выход…")
            return
        print("✔ игра на связи")
        try:
            run_levels(link, first, last, profiles, os.path.join(data_dir(), "run_build.json"))
        except KeyboardInterrupt:
            print("\n   (остановлено, данные сохранены)")
        build_report(cfg["project"])
        input("Enter — выход…")
    elif choice == "3":
        build_report(cfg["project"])
    else:
        return


if __name__ == "__main__":
    main()

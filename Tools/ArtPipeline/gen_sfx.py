# -*- coding: utf-8 -*-
"""Генерация звуков через ElevenLabs Sound Effects по Docs/AUDIO_PROMPTS_ВсёПоПолкам.txt.

  set ELEVEN_KEY=...   (ключ не хранится в проекте)
  python gen_sfx.py [--only sfx_pick,sfx_button] [--all] [--force]

Что делает: запрашивает PCM 44.1 кГц, обрезает тишину по краям, делает короткие фейды,
нормализует громкость по группам (UI тише, джинглы громче) и кладёт .wav в Audio/Sfx или Audio/Music.
"""
import argparse
import array
import json
import math
import os
import re
import sys
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(ROOT, "Assets", "AllOnShelves", "Audio")
PROMPTS = r"D:\Work\ЯндексИгры\Docs\AUDIO_PROMPTS_ВсёПоПолкам.txt"
KEY = os.environ.get("ELEVEN_KEY", "")
RATE = 44100

# звуки, которые реально вызывает код
USED = """sfx_button sfx_hover sfx_pick sfx_place_shelf sfx_place_cart sfx_belt_step sfx_set_sold sfx_nope
sfx_undo sfx_hint sfx_lock_open sfx_box_open sfx_spoil sfx_freezer_door sfx_customer_arrive sfx_customer_happy
sfx_customer_leave sfx_pallet_unload sfx_tape_rip sfx_popup_open sfx_popup_close sfx_coins_fly sfx_coin_tick
sfx_star_1 sfx_star_2 sfx_star_3 sfx_chest_open sfx_purchase_success sfx_renovate_build sfx_cart_tip
sfx_confetti sfx_combo_up sfx_jam_warning jingle_win jingle_lose jingle_new_mechanic jingle_stage_complete""".split()

# целевой пик по группам (мягкость прежде всего)
PEAK = {"ui": 0.30, "game": 0.55, "jingle": 0.75}


def group(name):
    if name.startswith("jingle"):
        return "jingle"
    if name in ("sfx_button", "sfx_hover", "sfx_popup_open", "sfx_popup_close", "sfx_undo", "sfx_hint", "sfx_nope", "sfx_coin_tick"):
        return "ui"
    return "game"


def parse():
    text = open(PROMPTS, encoding="utf-8").read().replace("\r\n", "\n")
    out = []
    for block in text.split("\n\n"):
        m = re.match(r"^(sfx_[a-z0-9_]+|jingle_[a-z0-9_]+)\.wav\n", block)
        if not m:
            continue
        name = m.group(1)
        style = re.search(r"^Style: (.+)$", block, re.M)
        dur = re.search(r"^Длительность: ([\d.,–—-]+)", block, re.M)
        seconds = 1.0
        if dur:
            nums = [float(x.replace(",", ".")) for x in re.findall(r"\d+(?:[.,]\d+)?", dur.group(1))]
            if nums:
                seconds = max(nums)  # берём верхнюю границу, лишнее обрежем
        out.append({"name": name, "prompt": style.group(1) if style else name, "seconds": seconds})
    return out


def request_pcm(prompt, seconds):
    body = json.dumps({
        "text": prompt,
        "duration_seconds": round(max(0.6, min(22.0, seconds)), 2),
        "prompt_influence": 0.6,
    }).encode()
    req = urllib.request.Request(
        "https://api.elevenlabs.io/v1/sound-generation?output_format=pcm_44100",
        data=body, headers={"xi-api-key": KEY, "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return array.array("h", r.read())


def trim(samples, want_seconds, head_db=-45.0, tail_db=-52.0):
    """Убирает тишину в начале и конце, обрезает по нужной длине, вешает фейды."""
    if not samples:
        return samples
    peak = max(abs(s) for s in samples) or 1
    head = peak * (10 ** (head_db / 20.0))
    tail = peak * (10 ** (tail_db / 20.0))
    a = 0
    while a < len(samples) and abs(samples[a]) < head:
        a += 1
    b = len(samples)
    while b > a and abs(samples[b - 1]) < tail:
        b -= 1
    a = max(0, a - int(0.004 * RATE))  # чуть-чуть воздуха перед атакой
    s = samples[a:b]
    # если сам звук начинается поздно (тихий «разгон» вначале) — начинаем от атаки перед пиком
    half = len(s) // 2
    if half:
        e1 = sum(float(x) * x for x in s[:half])
        e2 = sum(float(x) * x for x in s[half:]) + 1.0
        if e1 / (e1 + e2) < 0.3:
            top = max(range(len(s)), key=lambda i: abs(s[i]))
            gate = abs(s[top]) * 0.12
            quiet, onset = 0, 0
            for i in range(top, 0, -1):
                quiet = quiet + 1 if abs(s[i]) < gate else 0
                if quiet > int(0.015 * RATE):
                    onset = i
                    break
            s = s[max(0, onset - int(0.004 * RATE)):]
    limit = int(want_seconds * RATE)
    if len(s) > limit:  # длинный хвост режем и плавно гасим
        s = s[:limit]
    fi, fo = int(0.003 * RATE), min(int(0.03 * RATE), len(s) // 3)
    for i in range(min(fi, len(s))):
        s[i] = int(s[i] * i / fi)
    for i in range(fo):
        s[len(s) - 1 - i] = int(s[len(s) - 1 - i] * i / fo)
    return s


def normalize(samples, target):
    peak = max((abs(x) for x in samples), default=0)
    if peak == 0:
        return samples
    k = (target * 32767.0) / peak
    return array.array("h", [max(-32768, min(32767, int(x * k))) for x in samples])


def write_wav(path, samples):
    import wave
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(samples.tobytes())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    if not KEY:
        print("Нет ключа: set ELEVEN_KEY=...")
        return 1
    items = parse()
    if args.only:
        want = args.only.split(",")
        items = [i for i in items if i["name"] in want]
    elif not args.all:
        items = [i for i in items if i["name"] in USED]
    print(f"звуков к генерации: {len(items)}")
    for it in items:
        folder = "Music" if it["name"].startswith("jingle") else "Sfx"
        path = os.path.join(AUDIO, folder, it["name"] + ".wav")
        if os.path.exists(path) and not args.force:
            print("  есть:", it["name"])
            continue
        try:
            pcm = request_pcm(it["prompt"], it["seconds"])
        except Exception as e:
            print("  ОШИБКА", it["name"], e)
            continue
        s = trim(pcm, it["seconds"])
        s = normalize(s, PEAK[group(it["name"])])
        write_wav(path, s)
        print(f"  {it['name']}: {len(s) / RATE:.2f} c (запрошено {it['seconds']} c) -> {folder}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

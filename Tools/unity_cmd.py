# -*- coding: utf-8 -*-
"""Мини-клиент к мосту MCP for Unity (TCP, кадры: 8 байт big-endian длина + JSON).

Использование:
  python unity_cmd.py <command_type> '<json params>'
  python unity_cmd.py read_console '{"action":"get","types":["error"],"count":50}'
"""
import json
import socket
import struct
import sys

import glob
import os

PROJECT = "Yandex_Claude"


def port():
    for f in glob.glob(os.path.expanduser("~/.unity-mcp/unity-mcp-status-*.json")):
        try:
            d = json.load(open(f, encoding="utf-8"))
            if d.get("project_name") == PROJECT:
                return int(d["unity_port"])
        except Exception:
            pass
    return 6400


def send(cmd_type, params, timeout=300):
    s = socket.create_connection(("127.0.0.1", port()), timeout=timeout)
    buf = b""
    while b"\n" not in buf:
        buf += s.recv(1)
    payload = json.dumps({"type": cmd_type, "params": params}).encode("utf-8")
    s.sendall(struct.pack(">Q", len(payload)) + payload)

    def read_exact(n):
        data = b""
        while len(data) < n:
            chunk = s.recv(n - len(data))
            if not chunk:
                raise IOError("connection closed")
            data += chunk
        return data

    length = struct.unpack(">Q", read_exact(8))[0]
    resp = read_exact(length).decode("utf-8")
    s.close()
    return resp


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    t = sys.argv[1]
    p = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    print(send(t, p))

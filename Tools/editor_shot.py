# -*- coding: utf-8 -*-
"""Скриншот открытой сцены в режиме редактора (без Play), ровно 1920×1080:
канва временно рендерится камерой в текстуру, потом всё возвращается как было.

python editor_shot.py out.jpg [ширина_превью=1280]
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402
sys.stdout.reconfigure(encoding="utf-8")
out = os.path.abspath(sys.argv[1]).replace("\\", "/")
thumb = int(sys.argv[2]) if len(sys.argv) > 2 else 1280
png = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Temp", "editor_shot_raw.png").replace("\\", "/")
code = f'''
var canvases = new System.Collections.Generic.List<UnityEngine.Canvas>();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay) canvases.Add(c);
var go = new UnityEngine.GameObject("__shot_cam");
go.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
var cam = go.AddComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color(1f, 0.957f, 0.89f, 1f);
cam.cullingMask = 1 << 5;
cam.orthographic = true;
var rtex = new UnityEngine.RenderTexture(1920, 1080, 24);
cam.targetTexture = rtex;
foreach (var c in canvases) {{ c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 10f; }}
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
var prev = UnityEngine.RenderTexture.active;
UnityEngine.RenderTexture.active = rtex;
var tex = new UnityEngine.Texture2D(1920, 1080, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = prev;
foreach (var c in canvases) {{ c.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }}
cam.targetTexture = null;
UnityEngine.Object.DestroyImmediate(go);
UnityEngine.Object.DestroyImmediate(rtex);
System.IO.File.WriteAllBytes(@"{png}", UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(tex);
UnityEngine.Canvas.ForceUpdateCanvases();
return canvases.Count.ToString();
'''
r = json.loads(send("execute_code", {"action": "execute", "code": code}, timeout=60))
res = r.get("result", {})
if not res.get("success"):
    print("ERROR", json.dumps(res, ensure_ascii=False)[:800])
    sys.exit(1)
from PIL import Image
im = Image.open(png)
im.thumbnail((thumb, thumb))
im.convert("RGB").save(out, quality=86)
os.remove(png)
print("shot", out)

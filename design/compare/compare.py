"""Compare mockup and app screenshots of the same screen and size.

usage: python compare.py MOCKUP_DIR APP_DIR OUT_DIR [--threshold 0.04]
Files are matched by name (for example 1280x800-light-today.png). For each pair the
report shows the mockup, the app, and a map of the pixels that differ, with the share
of differing pixels. A pair above the threshold fails, and the script exits with 1.
"""
import argparse, html, os, sys
import numpy as np
from PIL import Image, ImageFilter

def load(path, size=None):
    image = Image.open(path).convert("RGB")
    return image.resize(size, Image.LANCZOS) if size and image.size != size else image

def diff(mock, app, tolerance):
    a = np.asarray(mock.filter(ImageFilter.GaussianBlur(1)), dtype=np.int16)
    b = np.asarray(app.filter(ImageFilter.GaussianBlur(1)), dtype=np.int16)
    delta = np.abs(a - b).max(axis=2)
    mask = delta > tolerance
    heat = np.asarray(app.convert("L").convert("RGB"), dtype=np.uint8) // 3 + 120
    heat[mask] = (230, 40, 40)
    return float(mask.mean()), Image.fromarray(heat.astype(np.uint8))

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mockup"); parser.add_argument("app"); parser.add_argument("out")
    parser.add_argument("--threshold", type=float, default=0.04)
    parser.add_argument("--tolerance", type=int, default=24)
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)
    names = sorted(n for n in os.listdir(args.mockup) if n.endswith(".png") and os.path.exists(os.path.join(args.app, n)))
    if not names:
        sys.exit("no screenshot pairs with the same name")
    rows, failed = [], 0
    for name in names:
        app = load(os.path.join(args.app, name))
        mock = load(os.path.join(args.mockup, name), app.size)
        share, heat = diff(mock, app, args.tolerance)
        stem = name[:-4]
        mock.save(os.path.join(args.out, stem + ".mockup.png"))
        app.save(os.path.join(args.out, stem + ".app.png"))
        heat.save(os.path.join(args.out, stem + ".diff.png"))
        ok = share <= args.threshold
        failed += not ok
        rows.append((stem, share, ok))
        print(f"{'ok  ' if ok else 'FAIL'} {stem}: {share:.1%} of pixels differ")
    body = "".join(
        f"<section><h2>{html.escape(s)} <span class={'ok' if ok else 'bad'}>{share:.1%}</span></h2>"
        f"<div class=row><figure><img src='{s}.mockup.png'><figcaption>Maquette</figcaption></figure>"
        f"<figure><img src='{s}.app.png'><figcaption>Application</figcaption></figure>"
        f"<figure><img src='{s}.diff.png'><figcaption>Pixels différents</figcaption></figure></div></section>"
        for s, share, ok in rows)
    with open(os.path.join(args.out, "report.html"), "w", encoding="utf-8") as f:
        f.write("<!doctype html><meta charset=utf-8><title>Maquette et application</title><style>"
                "body{font:14px system-ui;margin:20px;background:#f4f1ec}.row{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}"
                "img{width:100%;border:1px solid #ccc}figure{margin:0}.ok{color:#2F7D4F}.bad{color:#B4482B}</style>"
                f"<h1>Maquette et application, seuil {args.threshold:.0%}</h1>{body}")
    sys.exit(1 if failed else 0)

if __name__ == "__main__":
    main()

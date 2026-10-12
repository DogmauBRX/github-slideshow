#!/usr/bin/env python3
"""Zip the Space Engineers mod folder for release (no game files: Satisfactory's
Docs file is linked in on the player's PC by Melty, never shipped)."""
import hashlib, os, sys, zipfile
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VERSION = sys.argv[1] if len(sys.argv) > 1 else "0.1.0"
src = os.path.join(ROOT, "mod")
os.makedirs(os.path.join(ROOT, "dist"), exist_ok=True)
out = os.path.join(ROOT, "dist", f"FicsitInSpace-{VERSION}.zip")
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for base, _, files in os.walk(src):
        for f in sorted(files):
            p = os.path.join(base, f)
            rel = os.path.relpath(p, src).replace(os.sep, "/")
            if rel.startswith("FicsitInSpace/Satisfactory/"):
                sys.exit("refusing to package Satisfactory files: " + rel)
            z.write(p, rel)
            print("  +", rel)
data = open(out, "rb").read()
print(out, len(data), "bytes sha256", hashlib.sha256(data).hexdigest())

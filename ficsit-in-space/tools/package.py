#!/usr/bin/env python3
"""Zip the Space Engineers mod folder for release (no game files: Satisfactory's
Docs file is linked in on the player's PC by Melty, never shipped)."""
import hashlib, os, subprocess, sys, zipfile
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VERSION = sys.argv[1] if len(sys.argv) > 1 else "0.1.0"
src = os.path.join(ROOT, "mod")
# build the launcher (.NET Framework 4 exe, runs on stock Windows)
scripts = os.path.join(src, "FicsitInSpace", "Data", "Scripts", "FicsitInSpace")
os.makedirs(os.path.join(src, "FicsitInSpace", "Launcher"), exist_ok=True)
subprocess.run(["mcs", "-nologo", "-optimize", "-out:" + os.path.join(src, "FicsitInSpace", "Launcher", "FicsitLauncher.exe"),
                os.path.join(ROOT, "launcher", "FicsitLauncher.cs")] +
               [os.path.join(scripts, f) for f in ("SatDocs.cs", "MiniJson.cs", "SheetData.cs")], check=True)
os.makedirs(os.path.join(ROOT, "dist"), exist_ok=True)
out = os.path.join(ROOT, "dist", f"FicsitInSpace-{VERSION}.zip")
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for base, _, files in os.walk(src):
        for f in sorted(files):
            p = os.path.join(base, f)
            rel = os.path.relpath(p, src).replace(os.sep, "/")
            if rel.startswith("FicsitInSpace/Satisfactory/") or rel.endswith("launcher.log"):
                sys.exit("refusing to package Satisfactory files: " + rel)
            z.write(p, rel)
            print("  +", rel)
    z.write(os.path.join(ROOT, "README.md"), "FicsitInSpace/README.md")
    print("  + FicsitInSpace/README.md")
data = open(out, "rb").read()
print(out, len(data), "bytes sha256", hashlib.sha256(data).hexdigest())

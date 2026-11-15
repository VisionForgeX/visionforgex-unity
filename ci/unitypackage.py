#!/usr/bin/env python3
"""
Makes VisionForgeX-<version>.unitypackage, for games that do not use the Package Manager: the package's files under
Assets/VisionForgeX/Plugin, the demo under Assets/VisionForgeX/Demo. A .unitypackage is a gzipped tar with one folder
per asset (named by its GUID) holding the file, its .meta and its path in the project. No Unity needed.

    python3 ci/unitypackage.py [output folder]     (default: build/)
"""
import hashlib
import io
import json
import os
import re
import sys
import tarfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "Packages", "com.visionforgex.ads")


def entries():
    """(path in the game's project, file on disk or None for a folder, its .meta) for every asset."""
    out = [("Assets/VisionForgeX/Plugin", None, None)]   # the demo folder comes with its own .meta
    for d, dirs, files in os.walk(PKG):
        rel_d = os.path.relpath(d, PKG).replace(os.sep, "/")
        dirs[:] = sorted(x for x in dirs if x != "Tests" and not x.startswith("."))
        for x in dirs:
            rel = (rel_d + "/" + x).lstrip("./")
            if x.endswith("~"):
                continue
            out.append((target(rel), None, os.path.join(d, x + ".meta")))
        for f in sorted(files):
            if f.endswith(".meta") or f.startswith(".") or f == "package.json":
                continue
            rel = (rel_d + "/" + f).lstrip("./")
            out.append((target(rel), os.path.join(d, f), os.path.join(d, f + ".meta")))
    return out


def target(rel):
    if rel.startswith("Samples~/Demo"):
        return "Assets/VisionForgeX/Demo" + rel[len("Samples~/Demo"):]
    return "Assets/VisionForgeX/Plugin/" + rel


def add(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def folder_meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % guid).encode()


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "build")
    version = json.load(open(os.path.join(PKG, "package.json")))["version"]
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, "VisionForgeX-%s.unitypackage" % version)
    n = 0
    with tarfile.open(path, "w:gz") as tar:
        for name, src, meta in entries():
            if meta is None:
                guid = hashlib.md5(("unitypackage/" + name).encode()).hexdigest()
                meta_bytes = folder_meta(guid)
            else:
                meta_bytes = open(meta, "rb").read()
                guid = re.search(rb"^guid: ([0-9a-f]{32})$", meta_bytes, re.M).group(1).decode()
            add(tar, guid + "/pathname", name.encode())
            add(tar, guid + "/asset.meta", meta_bytes)
            if src is not None:
                add(tar, guid + "/asset", open(src, "rb").read())
            n += 1
    print("%s: %d assets" % (path, n))
    return 0


if __name__ == "__main__":
    sys.exit(main())

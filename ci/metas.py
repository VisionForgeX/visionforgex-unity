#!/usr/bin/env python3
"""
Unity needs a .meta file beside every file and folder of a package (a package installed from Git is read-only, so Unity
cannot make them). This script writes the missing ones and checks them all.

    python3 ci/metas.py          write missing .meta files (stable GUIDs: made from the path, so they never change)
    python3 ci/metas.py --check  fail when one is missing, a GUID is used twice, or a native plugin is set for the
                                 wrong platform (CI runs this)

Native plugins: .java/.kt under Plugins/Android are for Android only; .mm/.m/.swift/.h under Plugins/iOS for iOS only.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TREES = ["Packages/com.visionforgex.ads", "Assets/Editor"]
SKIP_FILES = {".gitkeep"}

TEXT = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
DEFAULT = "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
MONO = ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
ASMDEF = "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
PACKAGE = "PackageManifestImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def plugin(platform):
    """A PluginImporter for one platform only ("Android" or "iOS")."""
    android = 1 if platform == "Android" else 0
    ios = 1 if platform == "iOS" else 0
    return (
        "PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n"
        "  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n"
        "  platformData:\n"
        "  - first:\n      : Any\n    second:\n      enabled: 0\n      settings:\n        Exclude Android: %d\n        Exclude Editor: 1\n"
        "        Exclude Linux64: 1\n        Exclude OSXUniversal: 1\n        Exclude Win: 1\n        Exclude Win64: 1\n        Exclude iOS: %d\n"
        "  - first:\n      Android: Android\n    second:\n      enabled: %d\n      settings:\n        CPU: ARMv7\n"
        "  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n"
        "  - first:\n      Editor: Editor\n    second:\n      enabled: 0\n      settings:\n        DefaultValueInitialized: true\n"
        "  - first:\n      iPhone: iOS\n    second:\n      enabled: %d\n      settings:\n        AddToEmbeddedBinaries: false\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    ) % (1 - android, 1 - ios, android, ios)


def guid(rel):
    # stable: the same file always has the same GUID; the package's paths are written as installed
    key = rel.replace("Packages/com.visionforgex.ads/", "com.visionforgex.ads/")
    return hashlib.md5(key.encode()).hexdigest()


def importer(rel, is_dir):
    if is_dir:
        return "folderAsset: yes\n" + DEFAULT
    name = os.path.basename(rel)
    ext = os.path.splitext(name)[1].lower()
    if name == "package.json":
        return PACKAGE
    if ext == ".cs":
        return MONO
    if ext == ".asmdef":
        return ASMDEF
    if ext in (".java", ".kt") and "/Plugins/Android/" in rel:
        return plugin("Android")
    if ext in (".mm", ".m", ".swift", ".h") and "/Plugins/iOS/" in rel:
        return plugin("iOS")
    if ext in (".md", ".txt", ".json", ".xml"):
        return TEXT
    return DEFAULT


def walk():
    """Every file and folder that needs a .meta, as paths relative to the repository."""
    out = []
    for tree in TREES:
        base = os.path.join(ROOT, tree)
        if not os.path.isdir(base):
            continue
        if tree != "Packages/com.visionforgex.ads":
            out.append((tree, True))   # Assets/Editor itself
        for d, dirs, files in os.walk(base):
            dirs[:] = sorted(x for x in dirs if not x.startswith("."))
            rel_d = os.path.relpath(d, ROOT).replace(os.sep, "/")
            for x in dirs:
                out.append((rel_d + "/" + x, True))
            for f in sorted(files):
                if f.endswith(".meta") or f in SKIP_FILES or f.startswith("."):
                    continue
                out.append((rel_d + "/" + f, False))
    return out


def main():
    check = "--check" in sys.argv
    problems, written, seen = [], 0, {}
    for rel, is_dir in walk():
        # a folder ending in ~ (Samples~) is not imported by Unity and has no .meta; its contents are copied into a game
        # with their metas, so they have them
        if is_dir and rel.endswith("~"):
            continue
        meta = os.path.join(ROOT, rel + ".meta")
        expected_importer = importer(rel, is_dir)
        if not os.path.exists(meta):
            if check:
                problems.append("missing: " + rel + ".meta")
                continue
            with open(meta, "w", newline="\n") as f:
                f.write("fileFormatVersion: 2\nguid: " + guid(rel) + "\n" + expected_importer)
            written += 1
        text = open(meta).read()
        m = re.search(r"^guid: ([0-9a-f]{32})$", text, re.M)
        if not m:
            problems.append("no GUID in " + rel + ".meta")
            continue
        g = m.group(1)
        if g in seen:
            problems.append("GUID " + g + " used by both " + seen[g] + " and " + rel)
        seen[g] = rel
        first_importer = expected_importer.split(":")[0]
        if first_importer not in text and not is_dir:
            problems.append(rel + ".meta: expected " + first_importer)
        if "PluginImporter" in expected_importer:
            want_android = "/Plugins/Android/" in rel
            if want_android and not re.search(r"Android: Android\n    second:\n      enabled: 1", text):
                problems.append(rel + ".meta: not enabled for Android")
            if not want_android and not re.search(r"iPhone: iOS\n    second:\n      enabled: 1", text):
                problems.append(rel + ".meta: not enabled for iOS")
    # every .meta has its file
    for tree in TREES:
        for d, dirs, files in os.walk(os.path.join(ROOT, tree)):
            for f in files:
                if f.endswith(".meta") and not os.path.exists(os.path.join(d, f[:-5])):
                    problems.append("orphan: " + os.path.relpath(os.path.join(d, f), ROOT))
    for p in problems:
        print("  PROBLEM  " + p)
    print("%d .meta files checked, %d written, %d problems" % (len(seen), written, len(problems)))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())

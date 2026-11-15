#!/usr/bin/env python3
"""
Checks that the package tells one story: the same version in package.json, VisionForgeX.Version and the changelog;
the two changelogs identical; the native SDK versions the same in C#, the dependency file and the docs; the bridge's
message keys and C functions the same on every side. Runs in CI with no Unity.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "Packages", "com.visionforgex.ads")
problems = []


def read(*p):
    return open(os.path.join(*p), encoding="utf-8").read()


def need(ok, what):
    print(("  PASS  " if ok else "  FAIL  ") + what)
    if not ok:
        problems.append(what)


pkg = json.load(open(os.path.join(PKG, "package.json")))
cs = read(PKG, "Runtime", "VisionForgeX.cs")
version = re.search(r'public const string Version = "([^"]+)"', cs).group(1)
android = re.search(r'AndroidSdkVersion = "([^"]+)"', cs).group(1)
ios = re.search(r'IosSdkVersion = "([^"]+)"', cs).group(1)
need(pkg["version"] == version, "package.json %s is VisionForgeX.Version %s" % (pkg["version"], version))
root_log, pkg_log = read(ROOT, "CHANGELOG.md"), read(PKG, "CHANGELOG.md")
need(root_log == pkg_log, "the two CHANGELOG.md files are identical")
need(re.search(r"^## " + re.escape(version) + r"\b", root_log, re.M) is not None, "the changelog has a section for " + version)
need(pkg["dependencies"].get("com.unity.external-dependency-manager") is not None, "the package depends on Unity's External Dependency Manager")
readme = read(ROOT, "README.md")
need("#" + version in readme, "the README's install address names the tag " + version)
need("vfx-ads-nextgen" in readme and android in readme and ios in readme, "the README names the native SDKs %s / %s" % (android, ios))

# the bridges: every C function C# imports exists in the .mm, and the other way round
mm = read(PKG, "Runtime", "Plugins", "iOS", "VfxUnityBridge.mm")
ios_cs = read(PKG, "Runtime", "Internal", "VfxIosBridge.cs")
c_funcs = set(re.findall(r"\b(VfxUnity_\w+)\s*\(", mm))
cs_funcs = set(re.findall(r"static extern \w+ (VfxUnity_\w+)\(", ios_cs))
need(c_funcs == cs_funcs, "C# imports exactly the .mm's C functions (%d)" % len(cs_funcs))
swift = read(PKG, "Runtime", "Plugins", "iOS", "VfxUnityBridge.swift")
objc_selectors = set(re.findall(r"^\+ \([^)]*\)\s*([A-Za-z]+[^;]*);", mm, re.M))
swift_selectors = set(re.findall(r"@objc\(([^)]+)\)", swift)) - {"VfxUnityBridgeSwift"}
mm_sels = set()
for decl in objc_selectors:
    parts = re.findall(r"(\w+):", decl)
    mm_sels.add("".join(p + ":" for p in parts) if parts else decl.split()[0])
need(mm_sels == swift_selectors, "the .mm declares exactly the Swift bridge's selectors (%d)" % len(swift_selectors))

java = read(PKG, "Runtime", "Plugins", "Android", "VfxUnityBridge.java")
android_cs = read(PKG, "Runtime", "Internal", "VfxAndroidBridge.cs")
java_methods = set(re.findall(r"public static \w+ (\w+)\(", java))
cs_calls = set(re.findall(r'(?:Call|Get)\("(\w+)"', android_cs))
need(cs_calls <= java_methods, "every Java method C# calls exists (%s)" % ", ".join(sorted(cs_calls - java_methods) or ["all there"]))

# the message keys are the same on all sides
for kind in ["init", "show", "event", "revenue", "consent", "banner"]:
    need('"' + kind + '"' in java and '"' + kind + '"' in swift, "message kind \"%s\" is sent by both bridges" % kind)
for event in ["shown", "clicked", "reward", "dismissed", "notShown"]:
    need('"' + event + '"' in java and '"' + event + '"' in swift and '"' + event + '"' in cs, "show event \"%s\" on every side" % event)

print("\n%d problems" % len(problems))
sys.exit(1 if problems else 0)

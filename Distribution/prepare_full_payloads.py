#!/usr/bin/env python3
"""Pack local SDKs and stage the allowlisted UniWebView native export. Never exports the game."""
import argparse
import json
from pathlib import Path
import shutil
import tarfile

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "Builds/Packages"
SDK_VERSIONS = {
    "appsflyer-unity-plugin": "7.0.3",
    "com.google.external-dependency-manager": "1.2.189",
    "com.unity.mobile.notifications": "2.4.3",
    "com.unity.ugui": "2.0.0",
    "com.unity.inputsystem": "1.18.0",
}
VENDOR_ASSETS = [
    "Assets/UniWebView/Script", "Assets/UniWebView/Interface", "Assets/UniWebView/Editor",
    "Assets/UniWebView/UniWebView-CSharp.asmdef", "Assets/UniWebView/link.xml",
    "Assets/UniWebView/README.txt", "Assets/UniWebView/CHANGELOG.md",
    "Assets/Plugins/Android/UniWebView.aar", "Assets/Plugins/UniWebView.bundle",
    "Assets/Plugins/iOS/UniWebView.xcframework",
]


def keep_archive_member(info):
    parts = Path(info.name).parts
    if any(p in (".DS_Store", ".git", ".gitignore") for p in parts):
        return None
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    return info


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", type=Path, required=True)
    args = parser.parse_args()
    stage = args.stage.resolve()
    if stage.exists() and any(stage.iterdir()):
        raise ValueError("Use a fresh empty staging directory.")
    if stage == ROOT or stage == Path.home() or stage == Path("/"):
        raise ValueError("Unsafe staging path")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with tarfile.open(OUTPUT / "com.alienproject.webwrapper-0.6.0.tgz", "w:gz") as tar:
        def core_filter(info):
            if any(part in ("Tests", "Tests.meta") for part in Path(info.name).parts):
                return None
            return keep_archive_member(info)
        tar.add(ROOT / "Packages/com.alienproject.webwrapper", arcname="package", filter=core_filter)
    dependencies = {"com.unity.modules." + name: "1.0.0" for name in (
        "androidjni", "imgui", "jsonserialize", "unitywebrequest", "unitywebrequesttexture",
        "imageconversion", "audio", "ui", "uielements", "physics", "physics2d")}
    for name, version in SDK_VERSIONS.items():
        matches = [p for p in (ROOT / "Library/PackageCache").glob(name + "@*")
                   if json.loads((p / "package.json").read_text())["version"] == version]
        if len(matches) != 1:
            raise ValueError("Expected exactly one cached package: " + name)
        archive = OUTPUT / (name + "-" + version + ".tgz")
        with tarfile.open(archive, "w:gz") as tar:
            tar.add(matches[0], arcname="package", filter=keep_archive_member)
        if name in ("com.unity.ugui", "com.unity.inputsystem"):
            dependencies[name] = "file:" + str(archive)

    (stage / "Assets/Editor").mkdir(parents=True)
    (stage / "Packages").mkdir()
    (stage / "ProjectSettings").mkdir()
    (stage / "Packages/manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=2))
    shutil.copyfile(ROOT / "ProjectSettings/ProjectVersion.txt", stage / "ProjectSettings/ProjectVersion.txt")
    for name in VENDOR_ASSETS:
        source, destination = ROOT / name, stage / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            shutil.copytree(source, destination, ignore=shutil.ignore_patterns(".DS_Store"))
        else:
            shutil.copyfile(source, destination)
        shutil.copyfile(str(source) + ".meta", str(destination) + ".meta")
        for parent in Path(name).parents:
            if str(parent) in ("Assets", "."):
                break
            meta = ROOT / (str(parent) + ".meta")
            if meta.exists():
                shutil.copyfile(meta, stage / (str(parent) + ".meta"))

    export_list = ",\n".join(json.dumps(name) for name in VENDOR_ASSETS)
    (stage / "Assets/Editor/ExportVendor.cs").write_text('''using UnityEditor;
using UnityEngine;
public static class ExportVendor {
    public static void Run() {
        if (System.Type.GetType("UniWebView, UniWebView-CSharp") == null)
            throw new System.Exception("UniWebView did not compile in the clean staging project.");
        AssetDatabase.ExportPackage(new string[] {''' + export_list + '''},
            ''' + json.dumps(str(OUTPUT / "UniWebView-6.10.0.unitypackage")) + ''', ExportPackageOptions.Recurse);
        Debug.Log("VENDOR EXPORT PASSED: native Android/iOS/macOS SDK only; no game, demo or app settings.");
    }
}
''')
    print(stage)
    print("Run Unity -batchmode -nographics -quit -projectPath <stage> -executeMethod ExportVendor.Run")


if __name__ == "__main__":
    main()

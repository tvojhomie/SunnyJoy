#!/usr/bin/env python3
"""Inspect the delivered archive recursively without extracting or executing payloads."""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile

ROOT = Path(__file__).resolve().parent.parent
BUNDLE = ROOT / "Builds/Packages/AlienProject-WebWrapper-Full-0.6.0.unitypackage"
PREFIX = "Assets/AlienProject/WebWrapperInstaller"


def unity_assets(data):
    with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as tar:
        assets = {}
        for member in tar.getmembers():
            if member.name.endswith("/pathname"):
                name = tar.extractfile(member).read().decode("utf-8").rstrip("\x00\n")
                assert not name.startswith("/") and ".." not in PurePosixPath(name).parts, "Unsafe asset pathname"
                blob_name = member.name.rsplit("/", 1)[0] + "/asset"
                try:
                    blob = tar.extractfile(blob_name)
                except KeyError:
                    blob = None
                assets[name] = blob.read() if blob else None
        return assets


def main():
    contents = BUNDLE.read_bytes()
    assets = unity_assets(contents)
    for name in assets:
        assert name == PREFIX or name.startswith(PREFIX + "/") or PREFIX.startswith(name + "/"), "Unexpected top-level asset: " + name
    inventory = json.loads(assets[PREFIX + "/Payload/inventory.json"])
    assert len(inventory["payloads"]) == 9, "Incomplete SDK inventory"
    settings = (ROOT / "Assets/AlienProject/WebWrapper/Resources/WebWrapperSettings.asset").read_text()
    key = re.search(r"^  appsFlyerDevKey: (.+)$", settings, re.M)
    forbidden = [b"com.gameandjoy.sunnyjoygame", b"been-there-atlas", b"spinandreel.site",
                 b"application-test-qa.top", b"/Users/alexandrplastun", b"6820946065"]
    if key:
        forbidden.append(key.group(1).strip().encode())
    vendor_count = 0
    for payload in inventory["payloads"]:
        data = assets[PREFIX + "/Payload/" + payload["file"]]
        assert len(data) == payload["sizeBytes"]
        assert hashlib.sha256(data).hexdigest() == payload["sha256"]
        if payload["packageName"] == "UniWebView":
            vendor = unity_assets(data)
            for name, blob in vendor.items():
                valid = name.startswith("Assets/UniWebView/") or name.startswith("Assets/Plugins/UniWebView.bundle") or name.startswith("Assets/Plugins/iOS/UniWebView.xcframework") or name == "Assets/Plugins/Android/UniWebView.aar"
                parents = {"Assets", "Assets/Plugins", "Assets/Plugins/iOS", "Assets/Plugins/Android", "Assets/UniWebView"}
                assert valid or name in parents, "Unexpected native package asset: " + name
                assert "/Demo" not in name and not name.endswith(".unity"), "Demo scene in SDK"
                if blob:
                    assert not any(marker in blob for marker in forbidden), "Host-specific content in native package"
            vendor_count = len(vendor)
        else:
            with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as tar:
                package = json.load(tar.extractfile("package/package.json"))
                assert package["name"] == payload["packageName"]
                assert package["version"] == payload["version"]
                for member in tar.getmembers():
                    assert member.name.startswith("package/") or member.name == "package", payload["packageName"] + ": " + member.name
                    if not member.isfile():
                        continue
                    blob = tar.extractfile(member).read()
                    assert not any(marker in blob for marker in forbidden), "Host-specific content in package " + payload["packageName"]
                    if payload["packageName"] == "com.alienproject.webwrapper":
                        assert "/Tests/" not in member.name and not member.name.endswith((".unity", ".asset")), "Host/test asset in core"
    for name, blob in assets.items():
        if blob and not name.endswith(".bytes"):
            assert not any(marker in blob for marker in forbidden), "Host-specific installer content"
    print(json.dumps({"status": "PASS", "archive": BUNDLE.name, "bytes": len(contents),
                      "sha256": hashlib.sha256(contents).hexdigest(), "payloads": len(inventory["payloads"]),
                      "native_asset_records": vendor_count, "game_and_app_credentials": "excluded"}, indent=2))


if __name__ == "__main__":
    main()

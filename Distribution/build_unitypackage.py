#!/usr/bin/env python3
"""Stage clean Unity projects for a native installer export/import smoke check.

This helper does not start Unity, install dependencies, or edit the host project.
Use a fresh --stage directory for each mode, then run the printed Unity command.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shlex
import shutil
import sys


REPO = Path(__file__).resolve().parent.parent
VERSION = "0.6.0"
INSTALLER_ROOT = "Assets/AlienProject/WebWrapperInstaller"
INSTALLER_PATH = INSTALLER_ROOT + "/Editor/WebWrapperPackageInstaller.cs"
INSTALLER_CLASS = "AlienProject.WebWrapperInstaller.WebWrapperPackageInstaller"
VERIFIER_PATH = "Assets/Editor/WebWrapperDistributionVerifier.cs"
PAYLOADS = (
    ("com.alienproject.webwrapper", VERSION, "Builds/Packages/com.alienproject.webwrapper-0.6.0.tgz"),
    ("com.google.firebase.app", "13.17.0", "GooglePackages/com.google.firebase.app-13.17.0.tgz"),
    ("com.google.firebase.messaging", "13.17.0", "GooglePackages/com.google.firebase.messaging-13.17.0.tgz"),
    ("appsflyer-unity-plugin", "7.0.3", "Builds/Packages/appsflyer-unity-plugin-7.0.3.tgz"),
    ("com.google.external-dependency-manager", "1.2.189", "Builds/Packages/com.google.external-dependency-manager-1.2.189.tgz"),
    ("com.unity.mobile.notifications", "2.4.3", "Builds/Packages/com.unity.mobile.notifications-2.4.3.tgz"),
    ("com.unity.ugui", "2.0.0", "Builds/Packages/com.unity.ugui-2.0.0.tgz"),
    ("com.unity.inputsystem", "1.18.0", "Builds/Packages/com.unity.inputsystem-1.18.0.tgz"),
    ("UniWebView", "6.10.0", "Builds/Packages/UniWebView-6.10.0.unitypackage"),
)


def digest_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_file(path: Path) -> Path:
    if not path.is_file():
        raise ValueError(f"Required input is missing: {path}. Finish preparing installer sources/payloads first.")
    if path.stat().st_size == 0:
        raise ValueError(f"Required input is empty: {path}")
    return path


def inventory_and_sources() -> tuple[dict, list[tuple[Path, str]], bytes]:
    source = require_file(REPO / "Distribution/InstallerSource/Editor/WebWrapperPackageInstaller.cs")
    assembly_definition = require_file(REPO / "Distribution/InstallerSource/Editor/AlienProject.WebWrapper.Installer.Editor.asmdef")
    readme = require_file(REPO / "Distribution/InstallerSource/README.txt")
    source_text = source.read_text(encoding="utf-8-sig")
    if not re.search(r"\bnamespace\s+AlienProject\.WebWrapperInstaller\b", source_text) or not re.search(
        r"\bclass\s+WebWrapperPackageInstaller\b", source_text
    ):
        raise ValueError(f"Installer source must declare {INSTALLER_CLASS}.")

    copies = [
        (source, INSTALLER_PATH),
        (assembly_definition, INSTALLER_ROOT + "/Editor/" + assembly_definition.name),
        (readme, INSTALLER_ROOT + "/README.txt"),
    ]
    payloads = []
    for name, version, relative_source in PAYLOADS:
        path = require_file(REPO / relative_source)
        archive_name = path.name
        payloads.append({
            "packageName": name,
            "version": version,
            "archiveName": archive_name,
            "file": archive_name + ".bytes",
            "sizeBytes": path.stat().st_size,
            "sha256": digest_file(path),
        })
        copies.append((path, INSTALLER_ROOT + "/Payload/" + archive_name + ".bytes"))

    for payload in payloads:
        token = "@@SHA_" + payload["packageName"] + "@@"
        if token not in source_text:
            raise ValueError("Missing installer checksum placeholder: " + token)
        source_text = source_text.replace(token, payload["sha256"])
    if "@@SHA_" in source_text:
        raise ValueError("Unresolved checksum placeholder")
    generated_source = REPO / "Builds/Packages/InstallerSource/WebWrapperPackageInstaller.cs"
    generated_source.parent.mkdir(parents=True, exist_ok=True)
    generated_source.write_text(source_text, encoding="utf-8")
    copies[0] = (generated_source, INSTALLER_PATH)

    inventory = {
        "schemaVersion": 1,
        "installerVersion": VERSION,
        "installerClass": INSTALLER_CLASS,
        "payloads": payloads,
    }
    inventory_bytes = (json.dumps(inventory, indent=2, ensure_ascii=True) + "\n").encode("utf-8")
    return inventory, copies, inventory_bytes


def expected_files(copies: list[tuple[Path, str]], inventory_bytes: bytes) -> list[dict]:
    records = [
        {"path": destination, "sizeBytes": source.stat().st_size,
         "sha256": digest_file(source), "blob": destination.endswith(".bytes")}
        for source, destination in copies
    ]
    records.append({
        "path": INSTALLER_ROOT + "/Payload/inventory.json",
        "sizeBytes": len(inventory_bytes),
        "sha256": hashlib.sha256(inventory_bytes).hexdigest(),
        "blob": False,
    })
    return records


def csharp_literal(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def render_verifier(output: Path, mode: str, records: list[dict]) -> str:
    entries = ",\n".join(
        "        new ExpectedFile(" + csharp_literal(record["path"]) + ", " +
        csharp_literal(record["sha256"]) + ", " + str(record["sizeBytes"]) + "L, " +
        str(record["blob"]).lower() + ")" for record in records
    )
    values = {
        "@@OUTPUT@@": csharp_literal(str(output)),
        "@@MODE@@": csharp_literal(mode),
        "@@ROOT@@": csharp_literal(INSTALLER_ROOT),
        "@@INSTALLER@@": csharp_literal(INSTALLER_PATH),
        "@@CLASS@@": csharp_literal(INSTALLER_CLASS),
        "@@VERIFIER@@": csharp_literal(VERIFIER_PATH),
        "@@ENTRIES@@": entries,
    }
    source = VERIFIER_SOURCE
    for placeholder, value in values.items():
        source = source.replace(placeholder, value)
    return source


def stage_project(stage: Path, output: Path, mode: str) -> None:
    if stage == REPO or stage == Path.home() or stage == Path(stage.anchor):
        raise ValueError("--stage must identify a new dedicated staging directory, not a repository, home, or filesystem root.")
    if stage.exists() and (not stage.is_dir() or any(stage.iterdir())):
        raise ValueError(f"Stage directory must be empty: {stage}. Use a fresh directory; existing files will not be removed.")
    if output.suffix.lower() != ".unitypackage":
        raise ValueError("--output must end in .unitypackage.")
    if stage == output or stage in output.parents:
        raise ValueError("Keep --output outside the staging project so it cannot become an imported project asset.")
    if mode == "import":
        require_file(output)
    project_version = require_file(REPO / "ProjectSettings/ProjectVersion.txt")
    _, copies, inventory_bytes = inventory_and_sources()
    records = expected_files(copies, inventory_bytes)

    stage.mkdir(parents=True, exist_ok=True)
    (stage / "Packages").mkdir()
    (stage / "ProjectSettings").mkdir()
    (stage / "Library").mkdir()
    (stage / "Assets/Editor").mkdir(parents=True)
    (stage / "Packages/manifest.json").write_text('{"dependencies": {}}\n', encoding="utf-8")
    shutil.copyfile(project_version, stage / "ProjectSettings/ProjectVersion.txt")
    (stage / VERIFIER_PATH).write_text(render_verifier(output, mode, records), encoding="utf-8")

    if mode == "export":
        for source, destination in copies:
            target = stage / destination
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        (stage / INSTALLER_ROOT / "Payload/inventory.json").write_bytes(inventory_bytes)

    version_match = re.search(r"^m_EditorVersion:\s*(\S+)", project_version.read_text(encoding="utf-8"), re.MULTILINE)
    editor_version = version_match.group(1) if version_match else "<Unity-version>"
    editor = f"/Applications/Unity/Hub/Editor/{editor_version}/Unity.app/Contents/MacOS/Unity"
    command = [editor, "-batchmode", "-nographics", "-projectPath", str(stage)]
    if mode == "export":
        command.append("-quit")
    command.extend(["-executeMethod", "WebWrapperDistributionVerifier." + mode.capitalize(),
                    "-logFile", str(stage / "Library" / ("distribution-" + mode + ".log"))])
    print(f"Prepared {mode} project: {stage}")
    print(f"Unity package: {output}")
    print(f"Expected report: {stage / 'Library/distribution-report.json'}")
    print(f"Expected exported asset files: {len(records)} (plus Unity-generated .meta/folder records)")
    if mode == "import":
        print("Import stage contains only the verifier. Its Import method imports the archive and waits across compilation/domain reload; do not add -quit or -importPackage.")
    print("Unity was not started. Run:")
    print(shlex.join(command))


VERIFIER_SOURCE = r'''using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class WebWrapperDistributionVerifier
{
    private const string OutputPackage = @@OUTPUT@@;
    private const string StageMode = @@MODE@@;
    private const string InstallerRoot = @@ROOT@@;
    private const string InstallerScript = @@INSTALLER@@;
    private const string InstallerClass = @@CLASS@@;
    private const string VerifierScript = @@VERIFIER@@;
    private const string ImportPendingKey = "WebWrapperDistribution.Import.Pending";
    private const string ImportDeadlineKey = "WebWrapperDistribution.Import.Deadline";
    private static double _importQuietUntil;
    private static readonly ExpectedFile[] Expected =
    {
@@ENTRIES@@
    };

    public static void Export() { Run("export"); }
    public static void Import()
    {
        try
        {
            if (StageMode != "import") throw new InvalidOperationException("Prepare an import-mode staging project first.");
            if (SessionState.GetBool(ImportPendingKey, false))
            {
                StartImportMonitor();
                return;
            }
            if (File.Exists(InstallerScript))
                throw new InvalidOperationException("The import smoke project must not already contain the installer. Use a fresh stage.");
            if (!File.Exists(OutputPackage)) throw new FileNotFoundException("Unity package is missing: " + OutputPackage);

            SessionState.SetString(ImportDeadlineKey, DateTime.UtcNow.AddMinutes(3).Ticks.ToString(CultureInfo.InvariantCulture));
            SessionState.SetBool(ImportPendingKey, true);
            StartImportMonitor();
            Debug.Log("[WebWrapper Distribution] Importing archive; waiting for assets and compiled installer type.");
            AssetDatabase.ImportPackage(OutputPackage, false);
        }
        catch (Exception exception) { FailImport(exception.ToString()); }
    }

    [InitializeOnLoadMethod]
    private static void ResumeImportAfterReload()
    {
        if (StageMode == "import" && SessionState.GetBool(ImportPendingKey, false))
            EditorApplication.delayCall += StartImportMonitor;
    }

    private static void StartImportMonitor()
    {
        if (!SessionState.GetBool(ImportPendingKey, false)) return;
        _importQuietUntil = EditorApplication.timeSinceStartup + 1.0;
        EditorApplication.update -= PollImport;
        EditorApplication.update += PollImport;
        AssetDatabase.importPackageFailed -= OnImportFailed;
        AssetDatabase.importPackageFailed += OnImportFailed;
        AssetDatabase.importPackageCancelled -= OnImportCancelled;
        AssetDatabase.importPackageCancelled += OnImportCancelled;
    }

    private static void PollImport()
    {
        try
        {
            if (!SessionState.GetBool(ImportPendingKey, false)) return;
            if (!long.TryParse(SessionState.GetString(ImportDeadlineKey, ""), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var deadline) || DateTime.UtcNow.Ticks >= deadline)
            {
                FailImport("Timed out waiting for Unity package import, compilation, or the compiled installer type.");
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                !File.Exists(InstallerScript) || AssetDatabase.LoadAssetAtPath<MonoScript>(InstallerScript) == null ||
                !AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(InstallerClass, false) != null))
            {
                _importQuietUntil = EditorApplication.timeSinceStartup + 1.0;
                return;
            }
            if (EditorApplication.timeSinceStartup < _importQuietUntil) return;

            ClearImportMonitor();
            Run("import");
        }
        catch (Exception exception) { FailImport(exception.ToString()); }
    }

    private static void OnImportFailed(string packageName, string error)
    {
        if (SessionState.GetBool(ImportPendingKey, false)) FailImport("Unity import failed: " + error);
    }

    private static void OnImportCancelled(string packageName)
    {
        if (SessionState.GetBool(ImportPendingKey, false)) FailImport("Unity package import was cancelled.");
    }

    private static void ClearImportMonitor()
    {
        SessionState.SetBool(ImportPendingKey, false);
        SessionState.SetString(ImportDeadlineKey, "");
        EditorApplication.update -= PollImport;
        EditorApplication.delayCall -= StartImportMonitor;
        AssetDatabase.importPackageFailed -= OnImportFailed;
        AssetDatabase.importPackageCancelled -= OnImportCancelled;
    }

    private static void FailImport(string message)
    {
        ClearImportMonitor();
        Finish(new Report { mode = "import", success = false, message = message,
            outputPackage = OutputPackage, installerClass = InstallerClass, utc = DateTime.UtcNow.ToString("O") });
    }

    private static void Run(string mode)
    {
        var report = new Report { mode = mode, outputPackage = OutputPackage, installerClass = InstallerClass,
            utc = DateTime.UtcNow.ToString("O") };
        try
        {
            if (mode != StageMode) throw new InvalidOperationException("Verifier method does not match the prepared stage mode.");
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!Regex.IsMatch(File.ReadAllText(Path.Combine(projectRoot, "Packages/manifest.json")),
                    @"^\s*\{\s*""dependencies""\s*:\s*\{\s*\}\s*\}\s*$"))
                throw new InvalidOperationException("The smoke project manifest must remain dependency-free.");

            report.installerMonoScriptClassResolved = VerifyCompiledScript(InstallerScript, InstallerClass);
            VerifyCompiledScript(VerifierScript, typeof(WebWrapperDistributionVerifier).FullName);
            var allowed = new HashSet<string>(Expected.Select(item => item.path), StringComparer.Ordinal) { VerifierScript };
            foreach (var absolutePath in Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories))
            {
                var assetPath = "Assets/" + absolutePath.Substring(Application.dataPath.Length + 1).Replace('\\', '/');
                if (assetPath.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (!allowed.Contains(assetPath))
                    throw new InvalidOperationException("Unexpected project asset (raw SDK, scene or host settings are forbidden): " + assetPath);
            }

            var verified = new List<ExpectedFile>();
            foreach (var expected in Expected)
            {
                var absolutePath = Path.Combine(projectRoot, expected.path);
                if (!File.Exists(absolutePath)) throw new FileNotFoundException("Expected installer asset is missing: " + expected.path);
                if (new FileInfo(absolutePath).Length != expected.sizeBytes || HashFile(absolutePath) != expected.sha256)
                    throw new InvalidDataException("Installer asset bytes changed: " + expected.path);
                if (expected.blob)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(expected.path);
                    if (asset == null) throw new InvalidDataException("Binary payload did not import as a TextAsset: " + expected.path);
                    var bytes = asset.bytes;
                    if (bytes.LongLength != expected.sizeBytes || HashBytes(bytes) != expected.sha256)
                        throw new InvalidDataException("Unity's imported binary payload differs: " + expected.path);
                }
                verified.Add(expected);
            }
            report.files = verified.ToArray();

            if (mode == "export")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPackage));
                AssetDatabase.ExportPackage(InstallerRoot, OutputPackage, ExportPackageOptions.Recurse);
            }
            if (!File.Exists(OutputPackage) || new FileInfo(OutputPackage).Length == 0)
                throw new IOException("Unity package output is missing or empty.");
            report.packageSha256 = HashFile(OutputPackage);
            report.packageSizeBytes = new FileInfo(OutputPackage).Length;
            report.success = true;
            report.message = "Installer and verifier compile; all " + Expected.Length + " asset hashes match; no raw SDK or host assets are present.";
        }
        catch (Exception exception)
        {
            report.success = false;
            report.message = exception.ToString();
        }

        Finish(report);
    }

    private static void Finish(Report report)
    {
        var reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/distribution-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        if (report.success) Debug.Log("[WebWrapper Distribution] " + report.mode + " verified. Report: " + reportPath);
        else Debug.LogError("[WebWrapper Distribution] " + report.message);
        if (Application.isBatchMode) EditorApplication.Exit(report.success ? 0 : 1);
        else if (!report.success) throw new InvalidOperationException(report.message);
    }

    private static bool VerifyCompiledScript(string path, string expectedClass)
    {
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        if (script == null) throw new InvalidOperationException("Expected MonoScript asset is missing: " + path);
        var type = script.GetClass();
        if (type != null)
        {
            if (type.FullName != expectedClass)
                throw new InvalidOperationException("MonoScript resolved to an unexpected class: " + path);
            return true;
        }

        var matches = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(expectedClass, false))
            .Where(candidate => candidate != null).ToArray();
        if (matches.Length != 1 || !matches[0].IsAbstract || !matches[0].IsSealed)
            throw new InvalidOperationException("Script did not compile as one static " + expectedClass + ": " + path);
        return false;
    }

    private static string HashFile(string path)
    {
        using (var algorithm = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    [Serializable]
    private sealed class ExpectedFile
    {
        public string path;
        public string sha256;
        public long sizeBytes;
        public bool blob;
        public ExpectedFile(string path, string sha256, long sizeBytes, bool blob)
        { this.path = path; this.sha256 = sha256; this.sizeBytes = sizeBytes; this.blob = blob; }
    }

    [Serializable]
    private sealed class Report
    {
        public string mode;
        public bool success;
        public string message;
        public string utc;
        public string installerClass;
        public bool installerMonoScriptClassResolved;
        public string outputPackage;
        public string packageSha256;
        public long packageSizeBytes;
        public ExpectedFile[] files;
    }
}
'''


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", required=True, type=Path, help="Fresh empty Unity staging directory.")
    parser.add_argument("--output", type=Path, default=REPO / f"Builds/Packages/AlienProject-WebWrapper-Full-{VERSION}.unitypackage",
                        help="Unity package written by export/read by import (outside the stage).")
    parser.add_argument("--mode", choices=("export", "import"), default="export")
    args = parser.parse_args(argv)
    try:
        stage_project(args.stage.expanduser().resolve(), args.output.expanduser().resolve(), args.mode)
    except (OSError, ValueError) as exception:
        print(f"error: {exception}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

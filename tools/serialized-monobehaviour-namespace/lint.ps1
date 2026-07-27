[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Unity 6000.4.7f1 fails to bind a MonoScript whose class sits under a FILE-SCOPED namespace
# ("namespace X;") when that component is serialized into a scene or prefab. The component loads
# as a null script and the editor reports "The referenced script on this Behaviour is missing!".
#
# This is latent: a file-scoped MonoBehaviour that is only ever added at runtime via AddComponent
# never shows the symptom, so the repository accumulated 30 of them safely. The moment one is
# dropped into a scene or prefab it breaks, and the symptom (a warning, not an error) is easy to
# ignore for weeks. That is exactly what happened to AtlasCharacterStandeePresenter.
#
# Rule: a MonoBehaviour serialized in any scene or prefab must declare a BLOCK namespace.

$scriptRoots = @(@(
    (Join-Path $RepoRoot "Assets/_Game/Scripts")
) | Where-Object { Test-Path -LiteralPath $_ })

$serializedRoots = @(@(
    (Join-Path $RepoRoot "Assets/_Game/Scenes"),
    (Join-Path $RepoRoot "Assets/_Game/Prefabs"),
    (Join-Path $RepoRoot "Assets/_Game/UI"),
    (Join-Path $RepoRoot "Assets/Resources")
) | Where-Object { Test-Path -LiteralPath $_ })

if ($scriptRoots.Count -eq 0) {
    Write-Host "No script roots found; nothing to check." -ForegroundColor Yellow
    exit 0
}

# ── Collect every guid referenced by a scene or prefab ────────────────────────
$serializedGuids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$serializedOwner = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
$guidPattern = [regex]'guid:\s*([0-9a-f]{32})'

foreach ($root in $serializedRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File -Include *.unity, *.prefab -ErrorAction SilentlyContinue |
        ForEach-Object {
            $owner = [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/')
            foreach ($match in $guidPattern.Matches((Get-Content -LiteralPath $_.FullName -Raw))) {
                $guid = $match.Groups[1].Value
                if ($serializedGuids.Add($guid)) {
                    $serializedOwner[$guid] = $owner
                }
            }
        }
}

# ── Check every MonoBehaviour source ─────────────────────────────────────────
$failures = [System.Collections.Generic.List[string]]::new()
$fileScopedTotal = 0
$serializedTotal = 0

$monoPattern = [regex]'(?m)^\s*(?:public|internal|sealed|abstract|partial|\s)*class\s+\w+\s*:\s*MonoBehaviour\b'
$nsPattern = [regex]'(?m)^namespace\s+([\w.]+)\s*(;)?\s*$'

foreach ($root in $scriptRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue | ForEach-Object {
        $source = Get-Content -LiteralPath $_.FullName -Raw
        if (-not $monoPattern.IsMatch($source)) { return }

        $ns = $nsPattern.Match($source)
        if (-not $ns.Success) { return }
        $isFileScoped = $ns.Groups[2].Success
        if (-not $isFileScoped) { return }
        $fileScopedTotal++

        $metaPath = "$($_.FullName).meta"
        if (-not (Test-Path -LiteralPath $metaPath)) { return }
        $metaGuid = $guidPattern.Match((Get-Content -LiteralPath $metaPath -Raw))
        if (-not $metaGuid.Success) { return }
        $guid = $metaGuid.Groups[1].Value
        if (-not $serializedGuids.Contains($guid)) { return }

        $serializedTotal++
        $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/')
        $failures.Add(
            "SERIALIZED MONOBEHAVIOUR NAMESPACE FAIL file='$rel' namespace='$($ns.Groups[1].Value)' " +
            "serialized_in='$($serializedOwner[$guid])': this MonoBehaviour is serialized into a scene or prefab " +
            "but declares a file-scoped namespace ('namespace $($ns.Groups[1].Value);'). Unity 6000.4.7f1 leaves its " +
            "MonoScript class-null, so the component loads as 'The referenced script on this Behaviour is missing!' " +
            "and its serialized references are silently lost. Convert it to a block namespace " +
            "('namespace $($ns.Groups[1].Value) { ... }'). Runtime-only components added via AddComponent are exempt " +
            "because the broken binding never surfaces there.")
    }
}

Write-Host "Scanned MonoBehaviour sources: file-scoped=$fileScopedTotal, of which serialized=$serializedTotal."

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host $failure -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASS: Every MonoBehaviour serialized into a scene or prefab uses a block namespace." -ForegroundColor Green
exit 0

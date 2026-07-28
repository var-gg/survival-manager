[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# A USS url() that points at a file which does not exist renders as nothing. UITK logs no error and
# the panel simply loses that frame, divider, glow, or icon. TacticalWorkshop.uss shipped four such
# references at a directory that never existed, so the panel rendered unstyled for as long as nobody
# compared it against another screen.
#
# Icon routing (Check 9) covers authored content IconIds. This covers the other half: style-sheet
# asset references.

$styleRoots = @(@(
    (Join-Path $RepoRoot "Assets/_Game/UI"),
    (Join-Path $RepoRoot "Assets/_Game/Scripts")
) | Where-Object { Test-Path -LiteralPath $_ })

if ($styleRoots.Count -eq 0) {
    Write-Host "No style roots found; nothing to check." -ForegroundColor Yellow
    exit 0
}

$urlPattern = [regex]'url\(\s*"(?<url>[^"]+)"\s*\)'
$failures = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($root in $styleRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File -Include *.uss, *.uxml -ErrorAction SilentlyContinue | ForEach-Object {
        $styleRel = [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/')
        $text = Get-Content -LiteralPath $_.FullName -Raw
        foreach ($match in $urlPattern.Matches($text)) {
            $url = $match.Groups['url'].Value
            # Only project:// database paths are resolvable here; unity theme refs and data URIs are not ours.
            if (-not $url.StartsWith('project://database/', [System.StringComparison]::Ordinal)) { continue }
            $assetPath = $url.Substring('project://database/'.Length)
            # Strip Unity's optional sub-asset query (?fileID=...&guid=...).
            $query = $assetPath.IndexOf('?')
            if ($query -ge 0) { $assetPath = $assetPath.Substring(0, $query) }
            $checked++
            $full = Join-Path $RepoRoot $assetPath
            if (Test-Path -LiteralPath $full) { continue }

            $leaf = [System.IO.Path]::GetFileName($assetPath)
            $elsewhere = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot "Assets") -Recurse -File -Filter $leaf -ErrorAction SilentlyContinue |
                Select-Object -First 3 |
                ForEach-Object { [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/') })
            $hint = if ($elsewhere.Count -gt 0) {
                " The same file name exists at: $($elsewhere -join ', '). Repoint the url or move the asset."
            }
            else {
                " No file with that name exists anywhere under Assets. Add the asset or remove the rule."
            }

            $failures.Add(
                "USS ASSET REFERENCE FAIL style='$styleRel' url='$assetPath': the referenced asset does not exist. " +
                "UITK renders nothing and logs no error, so the surface silently loses this image.$hint")
        }
    }
}

Write-Host "Checked $checked project:// asset references in UITK style sheets and templates."

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host $failure -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASS: Every UITK style asset reference resolves to a file on disk." -ForegroundColor Green
exit 0

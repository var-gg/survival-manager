param(
    [string]$RepoRoot = "."
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

$repoRootPath = (Get-Location).Path
$docsRootPath = Join-Path $repoRootPath 'docs'
$registryPath = Join-Path $docsRootPath '00_governance/deprecated-docs-registry.md'

$errors = [System.Collections.Generic.List[string]]::new()

function Add-PolicyError {
    param([string]$Message)

    $script:errors.Add($Message)
}

function Normalize-RepoPath {
    param([string]$Path)

    return ($Path -replace '\\', '/')
}

function Get-FileStatus {
    param([string]$Path)

    $content = Get-Content -Raw $Path
    $match = [regex]::Match($content, '(?m)^- (?:상태|Status):\s*([A-Za-z_-]+)')
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

function Resolve-MarkdownReference {
    param(
        [string]$BaseDirectory,
        [string]$Reference
    )

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return $null
    }

    if ($Reference -match '^(https?|mailto):') {
        return $null
    }

    $trimmed = $Reference.Trim()
    if ([System.IO.Path]::IsPathRooted($trimmed)) {
        return [System.IO.Path]::GetFullPath($trimmed)
    }

    if ($trimmed -match '^(AGENTS\.md|docs/|tasks/|prompts/|tools/|\.agents/)') {
        return [System.IO.Path]::GetFullPath((Join-Path $repoRootPath $trimmed))
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $trimmed))
}

function Get-MarkdownReferences {
    param(
        [string]$Content,
        [string]$BaseDirectory
    )

    $references = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($match in [regex]::Matches($Content, '`([^`]+\.md)`')) {
        $resolved = Resolve-MarkdownReference -BaseDirectory $BaseDirectory -Reference $match.Groups[1].Value
        if ($resolved) {
            [void]$references.Add((Normalize-RepoPath $resolved))
        }
    }

    foreach ($match in [regex]::Matches($Content, '\(([^)]+\.md)\)')) {
        $resolved = Resolve-MarkdownReference -BaseDirectory $BaseDirectory -Reference $match.Groups[1].Value
        if ($resolved) {
            [void]$references.Add((Normalize-RepoPath $resolved))
        }
    }

    return $references
}

function Get-NearestIndexPath {
    param([string]$FilePath)

    $currentDirectory = Split-Path $FilePath -Parent
    $docsRoot = [System.IO.Path]::GetFullPath($docsRootPath)

    while ($currentDirectory.StartsWith($docsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidate = Join-Path $currentDirectory 'index.md'
        if ((Test-Path $candidate) -and ([System.IO.Path]::GetFullPath($candidate) -ne [System.IO.Path]::GetFullPath($FilePath))) {
            return $candidate
        }

        if ([System.IO.Path]::GetFullPath($currentDirectory) -eq $docsRoot) {
            break
        }

        $currentDirectory = Split-Path $currentDirectory -Parent
    }

    return $null
}

function Split-RegistryPaths {
    param([string]$Value)

    $items = @()
    foreach ($part in ($Value -split ';')) {
        $clean = $part.Trim().Trim('`')
        if (-not [string]::IsNullOrWhiteSpace($clean)) {
            $items += $clean
        }
    }

    return $items
}

$englishMetadataLabels = @(
    'Status',
    'Owner',
    'Last Updated',
    'Source of Truth',
    'Related Docs',
    'Applies To',
    'Task Name',
    'Related Paths',
    'Depends On',
    'Current Status',
    'Completed',
    'On Hold',
    'Issues',
    'Decisions',
    'Next Steps',
    'Goal',
    'Non-Goals',
    'Constraints',
    'Deliverables',
    'Done Criteria',
    'Milestones',
    'Approval Criteria',
    'Verification Commands',
    'Stop Conditions',
    'Execution Scope',
    'Working Method',
    'Scope Limits',
    'Documentation Update Rules',
    'Test And Smoke Rules',
    'Phase',
    'phase',
    'Scope',
    'scope'
)
$englishMetadataPattern = '^(?:' + (($englishMetadataLabels | ForEach-Object { [regex]::Escape($_) }) -join '|') + '):'

$humanFacingFiles = @(
    (Join-Path $repoRootPath 'AGENTS.md')
)
$humanFacingFiles += Get-ChildItem $docsRootPath -Recurse -File -Filter *.md | Select-Object -ExpandProperty FullName
$humanFacingFiles += Get-ChildItem (Join-Path $repoRootPath 'tasks') -Recurse -File -Filter *.md | Select-Object -ExpandProperty FullName

foreach ($filePath in $humanFacingFiles | Sort-Object -Unique) {
    $relativePath = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($repoRootPath, $filePath)))
    $content = Get-Content -Raw $filePath

    foreach ($line in ($content -split "`r?`n")) {
        if ($line -match '^- ' -and $line.Substring(2) -match $englishMetadataPattern) {
            Add-PolicyError "영어 메타데이터 키가 남아 있습니다: $relativePath -> $line"
        }
    }
}

$docsFiles = Get-ChildItem $docsRootPath -Recurse -File -Filter *.md
$indexFiles = $docsFiles | Where-Object { $_.Name -eq 'index.md' }

foreach ($docFile in $docsFiles) {
    $relativePath = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($repoRootPath, $docFile.FullName)))
    $status = Get-FileStatus -Path $docFile.FullName

    if ($status -eq 'deprecated') {
        Add-PolicyError "active tree에 deprecated 문서가 남아 있습니다: $relativePath"
    }

    if ($docFile.Name -in @('index.md', 'README.md')) {
        continue
    }

    $nearestIndex = Get-NearestIndexPath -FilePath $docFile.FullName
    if (-not $nearestIndex) {
        Add-PolicyError "상위 index를 찾을 수 없습니다: $relativePath"
        continue
    }

    $indexDirectory = Split-Path $nearestIndex -Parent
    $expectedReference = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($indexDirectory, $docFile.FullName)))
    $indexContent = Get-Content -Raw $nearestIndex
    $indexReferences = Get-MarkdownReferences -Content $indexContent -BaseDirectory $indexDirectory
    $expectedResolved = Normalize-RepoPath(([System.IO.Path]::GetFullPath($docFile.FullName)))

    if (-not $indexReferences.Contains($expectedResolved)) {
        $indexRelativePath = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($repoRootPath, $nearestIndex)))
        Add-PolicyError "index coverage 누락: $relativePath -> $indexRelativePath 에 `$expectedReference` 항목이 없습니다."
    }
}

foreach ($indexFile in $indexFiles) {
    $indexDirectory = Split-Path $indexFile.FullName -Parent
    $indexRelativePath = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($repoRootPath, $indexFile.FullName)))
    $content = Get-Content -Raw $indexFile.FullName
    $references = Get-MarkdownReferences -Content $content -BaseDirectory $indexDirectory

    foreach ($reference in $references) {
        $referenceRelativePath = Normalize-RepoPath(([System.IO.Path]::GetRelativePath($repoRootPath, $reference)))

        if (-not (Test-Path $reference)) {
            Add-PolicyError "index가 존재하지 않는 Markdown을 가리킵니다: $indexRelativePath -> $referenceRelativePath"
            continue
        }

        $referenceStatus = Get-FileStatus -Path $reference
        if ($referenceStatus -eq 'deprecated') {
            Add-PolicyError "active index가 deprecated 문서를 노출합니다: $indexRelativePath -> $referenceRelativePath"
        }
    }
}

if (-not (Test-Path $registryPath)) {
    Add-PolicyError "registry 문서가 없습니다: docs/00_governance/deprecated-docs-registry.md"
}
else {
    $registryLines = Get-Content $registryPath
    $rows = $registryLines | Where-Object {
        $_ -match '^\|' -and
        $_ -notmatch '^\| ---' -and
        $_ -notmatch '^\| 이전경로 '
    }

    foreach ($row in $rows) {
        $parts = $row.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        if ($parts.Count -lt 7) {
            Add-PolicyError "registry row 형식이 잘못되었습니다: $row"
            continue
        }

        $oldPath = $parts[0].Trim('`')
        $replacementPaths = Split-RegistryPaths -Value $parts[1]
        $decisionPaths = Split-RegistryPaths -Value $parts[2]
        $removeBy = $parts[4]
        $action = $parts[5]

        $oldFullPath = Join-Path $repoRootPath $oldPath
        if ($action -eq 'removed' -and (Test-Path $oldFullPath)) {
            Add-PolicyError "registry에는 removed인데 원본 파일이 남아 있습니다: $oldPath"
        }

        if ($action -eq 'grace') {
            try {
                [void][datetime]::ParseExact($removeBy, 'yyyy-MM-dd', $null)
            }
            catch {
                Add-PolicyError "grace registry row에 remove_by 날짜가 없거나 형식이 잘못되었습니다: $oldPath"
            }
        }

        foreach ($replacementPath in $replacementPaths) {
            $replacementFullPath = Join-Path $repoRootPath $replacementPath
            if (-not (Test-Path $replacementFullPath)) {
                Add-PolicyError "registry replacement 경로가 존재하지 않습니다: $oldPath -> $replacementPath"
            }
        }

        foreach ($decisionPath in $decisionPaths) {
            $decisionFullPath = Join-Path $repoRootPath $decisionPath
            if (-not (Test-Path $decisionFullPath)) {
                Add-PolicyError "registry 결정기록 경로가 존재하지 않습니다: $oldPath -> $decisionPath"
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host 'Docs policy check failed:' -ForegroundColor Red
    foreach ($errorMessage in $errors) {
        Write-Host "- $errorMessage" -ForegroundColor Red
    }

    exit 1
}

Write-Host 'Docs policy check passed.'

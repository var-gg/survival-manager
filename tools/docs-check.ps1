param(
    [string]$RepoRoot = ".",
    [switch]$PolicyOnly,
    [int]$LinkCheckTimeoutSeconds = 30,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Paths
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot
$generatedMarkdownIgnores = @('PINDOC.md')

function Resolve-NpxPath {
    foreach ($commandName in @('npx.cmd', 'npx.exe', 'npx')) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw 'npx executable not found.'
}

function Invoke-MarkdownLinkCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Target,
        [Parameter(Mandatory = $true)]
        [string]$NpxPath,
        [int]$TimeoutSeconds = 30
    )

    $process = Start-Process -FilePath $NpxPath -ArgumentList @('--yes', 'markdown-link-check', $Target, '--quiet') -PassThru -NoNewWindow

    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try {
            $process.Kill($true)
        }
        catch {
        }

        Write-Warning "markdown-link-check timed out after ${TimeoutSeconds}s: $Target"
        return 124
    }

    return $process.ExitCode
}

function Remove-GeneratedMarkdownTargets {
    param(
        [string[]]$Targets
    )

    return @($Targets | Where-Object {
        (Split-Path -Leaf $_) -notin $generatedMarkdownIgnores
    })
}

Write-Host '== docs-policy-check =='
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .

if ($PolicyOnly) {
    exit 0
}

Write-Host '== markdownlint =='
$markdownTargets = @()

if ($Paths.Count -gt 0) {
    foreach ($path in $Paths) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $resolvedPaths = Resolve-Path -Path $path -ErrorAction SilentlyContinue
        foreach ($resolvedPath in $resolvedPaths) {
            $item = Get-Item $resolvedPath.Path
            if ($item.PSIsContainer) {
                $markdownTargets += Get-ChildItem -Path $item.FullName -Recurse -File -Filter *.md | Select-Object -ExpandProperty FullName
            }
            elseif ($item.Extension -eq '.md') {
                $markdownTargets += $item.FullName
            }
        }
    }

    $markdownTargets = @(Remove-GeneratedMarkdownTargets -Targets $markdownTargets | Sort-Object -Unique)
    if ($markdownTargets.Count -eq 0) {
        Write-Host 'No markdown targets selected. Skipping markdownlint and link check.'
        exit 0
    }

    Write-Host "Targeted markdown files: $($markdownTargets.Count)"
    npx --yes markdownlint-cli2 @markdownTargets
}
else {
    npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**" "#PINDOC.md"
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host '== markdown-link-check =='
if ($Paths.Count -eq 0) {
    $markdownTargets = @()
    $markdownTargets += Get-ChildItem . -File -Filter *.md | Select-Object -ExpandProperty FullName
    $markdownTargets += Get-ChildItem docs -Recurse -File -Filter *.md | Select-Object -ExpandProperty FullName
    $markdownTargets = @(Remove-GeneratedMarkdownTargets -Targets $markdownTargets | Sort-Object -Unique)
}

$npxPath = Resolve-NpxPath
$linkCheckFailed = $false
foreach ($target in $markdownTargets) {
    $exitCode = Invoke-MarkdownLinkCheck -Target $target -NpxPath $npxPath -TimeoutSeconds $LinkCheckTimeoutSeconds
    if ($exitCode -ne 0) {
        $linkCheckFailed = $true
    }
}

if ($linkCheckFailed) {
    exit 1
}

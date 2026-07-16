param(
    [string]$OutputDirectory = 'Logs/h100-concept-catalog'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$previous = Get-Item -LiteralPath 'Env:SM_H100_CONCEPT_CATALOG_OUTPUT' -ErrorAction SilentlyContinue

try {
    Set-Item -LiteralPath 'Env:SM_H100_CONCEPT_CATALOG_OUTPUT' -Value $OutputDirectory
    & pwsh -File $executeMethod `
        -Method 'SM.Editor.Validation.H100ConceptCatalogRunner.RunFromCli' `
        -LogFile 'Logs/h100-concept-catalog-ci.log' `
        -PhaseName 'H100 BT1-E03 concept catalog derivation' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 concept catalog executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $artifactPath = Join-Path $resolvedOutput 'concept_catalog_bt1.json'
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "H100 concept catalog artifact missing: $artifactPath"
    }

    $catalog = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
    if ($catalog.schema_version -ne 'concept-catalog-bt1-v1') {
        throw "Unexpected concept catalog schema: $($catalog.schema_version)"
    }
    if (-not [bool]$catalog.ratification_pending) {
        throw 'Owner anchor draft must remain ratification_pending.'
    }
    if (@($catalog.owner_anchors).Count -ne 10 -or @($catalog.anchor_derivations).Count -ne 10) {
        throw "Concept catalog must process exactly 10 owner anchors (anchors=$(@($catalog.owner_anchors).Count), derivations=$(@($catalog.anchor_derivations).Count))."
    }
    foreach ($derivation in @($catalog.anchor_derivations)) {
        $hasRecipe = [int]$derivation.legal_recipe_count -gt 0
        if ($hasRecipe -eq [bool]$derivation.derivation_gap) {
            throw "Anchor $($derivation.anchor_id) must have legal recipes or an explicit derivation_gap."
        }
    }
    if ([int]$catalog.summary.unreachable_threshold_reference_count -ne 0) {
        throw "Concept catalog references unreachable thresholds: $($catalog.summary.unreachable_threshold_reference_count)"
    }
    if ([int]$catalog.summary.unobservable_payoff_witness_count -ne 0) {
        throw "Concept catalog uses unobservable payoff witnesses: $($catalog.summary.unobservable_payoff_witness_count)"
    }
    if ([int]$catalog.summary.isomorphic_duplicate_count -le 0) {
        throw 'Concept catalog did not demonstrate isomorphic recipe deduplication.'
    }

    Write-Host "H100 concept catalog artifact: $artifactPath"
    foreach ($derivation in @($catalog.anchor_derivations)) {
        $result = if ([bool]$derivation.derivation_gap) { 'derivation_gap' } else { "recipes=$($derivation.legal_recipe_count) variants=$(@($derivation.variants).Count)" }
        Write-Host "  $($derivation.anchor_id): $result"
    }
    Write-Host "System-derived medoids: $(@($catalog.system_derived_medoids).Count)"
    Write-Host "Tier distribution: core=$($catalog.summary.core_variant_count), aspirational=$($catalog.summary.aspirational_variant_count)"
    Write-Host "Isomorphic duplicates removed: $($catalog.summary.isomorphic_duplicate_count); raw-stat-only subjects excluded: $($catalog.summary.raw_stat_only_excluded_count)"
}
finally {
    if ($null -ne $previous) {
        Set-Item -LiteralPath 'Env:SM_H100_CONCEPT_CATALOG_OUTPUT' -Value $previous.Value
    }
    else {
        Remove-Item -LiteralPath 'Env:SM_H100_CONCEPT_CATALOG_OUTPUT' -ErrorAction SilentlyContinue
    }
}

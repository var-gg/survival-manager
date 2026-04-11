#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Narrative authoring document validator.
.DESCRIPTION
    Validates master-script.md and dialogue-event-schema.md for structural
    correctness, cross-references, alias validity, and prose constraints.
#>
param()
$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'narrative_validate.py')
exit $LASTEXITCODE

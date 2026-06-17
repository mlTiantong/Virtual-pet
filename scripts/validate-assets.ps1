$ErrorActionPreference = "Stop"

$assetRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\src\DesktopPet.App\assets")).Path
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $assetRoot "animation-manifest.json"
$propManifestPath = Join-Path $assetRoot "prop-manifest.m8.json"
$motionSeqPath = Join-Path $assetRoot "motion-sequence.m8.json"
$xamlPath = Join-Path $projectRoot "src\DesktopPet.App\PetWindow.xaml"

if (!(Test-Path $manifestPath)) { throw "Missing animation-manifest.json" }

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$missing = New-Object System.Collections.Generic.List[string]
$usedImages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Get-AssetRelativePath {
    param([string]$FullPath)

    $base = [System.IO.Path]::GetFullPath($assetRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $full = [System.IO.Path]::GetFullPath($FullPath)

    if ($full.StartsWith($base, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($base.Length).TrimStart(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
    }

    return $full
}

function Add-AssetReference {
    param(
        [string]$Context,
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return }
    $normalized = $RelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
    $path = Join-Path $assetRoot $normalized
    $canonicalRelative = Get-AssetRelativePath $path
    [void]$usedImages.Add($canonicalRelative)

    if (!(Test-Path $path)) {
        $missing.Add("$Context -> $RelativePath")
    }
}

if (-not $manifest.animations.PSObject.Properties[$manifest.defaultAnimation]) {
    $missing.Add("defaultAnimation -> $($manifest.defaultAnimation)")
}

foreach ($prop in $manifest.animations.PSObject.Properties) {
    $animationId = $prop.Name
    $spec = $prop.Value
    if ($spec.sheet) {
        Add-AssetReference "animation-manifest.sheet: $animationId" $spec.sheet
    }

    foreach ($frame in $spec.frames) {
        Add-AssetReference "animation-manifest.frame: $animationId" $frame
    }
}

if (Test-Path $xamlPath) {
    $xaml = Get-Content $xamlPath -Raw -Encoding UTF8
    $matches = [regex]::Matches($xaml, 'Source="assets/([^"]+\.(png|jpg|jpeg|webp|gif|bmp))"', "IgnoreCase")
    foreach ($match in $matches) {
        Add-AssetReference "xaml image" $match.Groups[1].Value
    }
}

$definedProps = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path $propManifestPath) {
    $propManifest = Get-Content $propManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $propManifest.props.PSObject.Properties) {
        if (-not $p.Value.id) { $missing.Add("prop-manifest: $($p.Name) missing id") }
        if (-not $p.Value.sheet) {
            $missing.Add("prop-manifest: $($p.Name) missing sheet")
        } else {
            Add-AssetReference "prop-manifest: $($p.Name)" $p.Value.sheet
        }
        [void]$definedProps.Add($p.Name)
    }
}

$seqCount = 0
if (Test-Path $motionSeqPath) {
    $motionSeq = Get-Content $motionSeqPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($s in $motionSeq.sequences.PSObject.Properties) {
        $seqCount++
        $stepIdx = 0
        foreach ($step in $s.Value.steps) {
            if ($step.animation -and -not $manifest.animations.PSObject.Properties[$step.animation]) {
                $missing.Add("motion-sequence: $($s.Name) step $stepIdx unknown animation $($step.animation)")
            }
            if ($step.prop -and -not $definedProps.Contains([string]$step.prop)) {
                $missing.Add("motion-sequence: $($s.Name) step $stepIdx unknown prop $($step.prop)")
            }
            $stepIdx++
        }
    }
}

$imageExtensions = @(".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp")
$allImages = Get-ChildItem $assetRoot -Recurse -File |
    Where-Object { $imageExtensions -contains $_.Extension.ToLowerInvariant() }

$unreferenced = New-Object System.Collections.Generic.List[string]
foreach ($image in $allImages) {
    $relative = Get-AssetRelativePath $image.FullName
    if (-not $usedImages.Contains($relative)) {
        $unreferenced.Add($relative)
    }
}

if ($allImages.Count -ne 1) {
    $missing.Add("expected exactly one image asset, found $($allImages.Count)")
}

if ($allImages.Count -eq 1) {
    $referenceImage = Get-AssetRelativePath $allImages[0].FullName
    $referencePrefix = "reference$([System.IO.Path]::DirectorySeparatorChar)"
    if (-not $referenceImage.StartsWith($referencePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $missing.Add("reference image must live under reference -> $referenceImage")
    }
    if (-not $usedImages.Contains($referenceImage)) {
        $missing.Add("reference image is not used -> $referenceImage")
    }
}

if ($unreferenced.Count -gt 0) {
    foreach ($path in $unreferenced) {
        $missing.Add("unreferenced image -> $path")
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Asset validation failed:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Asset validation passed." -ForegroundColor Green
Write-Host "Animation count: $(@($manifest.animations.PSObject.Properties).Count)"
Write-Host "Image references: $($usedImages.Count)"
Write-Host "Motion sequences: $seqCount"
Write-Host "Props defined: $($definedProps.Count)"

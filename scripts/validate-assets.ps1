$ErrorActionPreference = "Stop"
$assetRoot = Join-Path $PSScriptRoot "..\src\DesktopPet.App\assets"
$manifestPath = Join-Path $assetRoot "animation-manifest.json"
$sourceManifestPath = Join-Path $assetRoot "source_green_manifest.json"

if (!(Test-Path $manifestPath)) { throw "Missing animation-manifest.json" }
if (!(Test-Path $sourceManifestPath)) { throw "Missing source_green_manifest.json" }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$sourceManifest = Get-Content $sourceManifestPath -Raw | ConvertFrom-Json
$missing = New-Object System.Collections.Generic.List[string]

foreach ($prop in $manifest.animations.PSObject.Properties) {
    $animationId = $prop.Name
    foreach ($frame in $prop.Value.frames) {
        $framePath = Join-Path $assetRoot $frame
        if (!(Test-Path $framePath)) {
            $missing.Add("animation-manifest: $animationId -> $frame")
        }
    }
}

foreach ($prop in $sourceManifest.source_files.PSObject.Properties) {
    $sourcePath = Join-Path $assetRoot $prop.Value
    if (!(Test-Path $sourcePath)) {
        $missing.Add("source_green_manifest.source_files: $($prop.Name) -> $($prop.Value)")
    }
}

foreach ($prop in $sourceManifest.keyed_files.PSObject.Properties) {
    $keyedPath = Join-Path $assetRoot $prop.Value
    if (!(Test-Path $keyedPath)) {
        $missing.Add("source_green_manifest.keyed_files: $($prop.Name) -> $($prop.Value)")
    }
}

foreach ($prop in $sourceManifest.animation_to_source.PSObject.Properties) {
    $animationPath = Join-Path $assetRoot $prop.Name
    if (!(Test-Path $animationPath)) {
        $missing.Add("source_green_manifest.animation_to_source: $($prop.Name)")
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Missing asset references:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Asset validation passed." -ForegroundColor Green
Write-Host "Animation count: $($manifest.animations.PSObject.Properties.Count)"
Write-Host "Green source count: $($sourceManifest.source_files.PSObject.Properties.Count)"
Write-Host "Animation to source mappings: $($sourceManifest.animation_to_source.PSObject.Properties.Count)"

# --- M8 config validation ---
$motionSeqPath = Join-Path $assetRoot "motion-sequence.m8.json"
$propManifestPath = Join-Path $assetRoot "prop-manifest.m8.json"

if (Test-Path $motionSeqPath) {
    $motionSeq = Get-Content $motionSeqPath -Raw | ConvertFrom-Json
    $seqCount = 0
    foreach ($s in $motionSeq.sequences.PSObject.Properties) {
        $seqCount++
        $stepIdx = 0
        foreach ($step in $s.Value.steps) {
            if ($step.animation -and $step.animation -as [string] -ne '') {
                if (-not $manifest.animations.PSObject.Properties[$step.animation]) {
                    Write-Warning "motion-sequence: '$($s.Name)' step $stepIdx references unknown animation '$($step.animation)'"
                }
            }
            $stepIdx++
        }
    }
    Write-Host "Motion sequences: $seqCount" -ForegroundColor Green
} else {
    Write-Warning "motion-sequence.m8.json not found (M8 sequences will fallback to direct PlayAnimation)"
}

if (Test-Path $propManifestPath) {
    $propManifest = Get-Content $propManifestPath -Raw | ConvertFrom-Json
    $propCount = 0
    foreach ($p in $propManifest.props.PSObject.Properties) {
        if (-not $p.Value.id) { Write-Warning "prop-manifest: prop entry missing 'id'" }
        if (-not $p.Value.sheet) { Write-Warning "prop-manifest: '$($p.Name)' missing 'sheet'" }
        $propCount++
    }
    Write-Host "Props defined: $propCount" -ForegroundColor Green
} else {
    Write-Warning "prop-manifest.m8.json not found (prop layer will be empty)"
}
# --- End M8 config validation ---

# SIG # Begin signature block
# MIIFdgYJKoZIhvcNAQcCoIIFZzCCBWMCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUlbvEb9NcSC5UahmOhdxRolvY
# dA+gggMOMIIDCjCCAfKgAwIBAgIQevXj86fnvppEOdU15nAzJzANBgkqhkiG9w0B
# AQsFADAdMRswGQYDVQQDDBJEZXNrdG9wUGV0IFNpZ25pbmcwHhcNMjYwNjEyMDYy
# ODQ5WhcNMjcwNjEyMDY0ODQ5WjAdMRswGQYDVQQDDBJEZXNrdG9wUGV0IFNpZ25p
# bmcwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCzRrBWQIdylTvjjnds
# P70PCHcS7wnZMWBsd/gzS07EdNT7YsO+7/59ee3Z2NsUqqAFJnl3F0C8QuZXAB38
# ToQD9HafNc9GlICc6OcMZHRIbiYoTpVYZeIkN+SpjGc6BVa8zK0yRU+BV66pQK/C
# nZcOVYSrlmkjr791fIge6FxnxmP1LRiHvWkx2QRykn/LlpqpGtqwvmd60taDS15W
# 25+QC/RzppqvM6B/LIYt2Fcw0zAKqGTI/pfXWArSYkccTp2ay6IMwDy6TnuxEJ+w
# 6F3UiGPo6kd/Z21yfVwUgLgdz6wyW8AlIKmupHRYQQ+w+DVc7swne4vy7HyFfXf/
# YD19AgMBAAGjRjBEMA4GA1UdDwEB/wQEAwIHgDATBgNVHSUEDDAKBggrBgEFBQcD
# AzAdBgNVHQ4EFgQU9Rj3eeeNLCjQ1r+boIgG5/D3Z1QwDQYJKoZIhvcNAQELBQAD
# ggEBABv3Uo91bIkGTOz+8cqD8y2AiBqmAxhxPiBOZyxPx8+MdvzzSzrU1Q6dNAZI
# Ex6h1k3tD1NArNax6J4gPJzqLxbpEvBr124UsYoA5v7Wauv5WhO4au9nO4NOaQ/b
# tHdw+BIjJXnhJ8rturV3ajU3iY3PYnk+Oa2yU6cDkoMe8kt9XwpVWpw/ub/wDD6n
# 5fdaR1s7cspRkwnhZuwea6PuqCx3TzCd5W1pIyLjj3KUm+q44MwJxemUwrwxAGA3
# AwtfpbT/CN4R9xCkFDv+3ZJ0xsAGg8tyVejhfhIFS7DVzzTgIiN/nnaAcPid64sg
# awA2Gnai3L6+XptHgT7Ceg199SYxggHSMIIBzgIBATAxMB0xGzAZBgNVBAMMEkRl
# c2t0b3BQZXQgU2lnbmluZwIQevXj86fnvppEOdU15nAzJzAJBgUrDgMCGgUAoHgw
# GAYKKwYBBAGCNwIBDDEKMAigAoAAoQKAADAZBgkqhkiG9w0BCQMxDAYKKwYBBAGC
# NwIBBDAcBgorBgEEAYI3AgELMQ4wDAYKKwYBBAGCNwIBFTAjBgkqhkiG9w0BCQQx
# FgQUTU6gtZi7HC+CmYhY/QS7FJ4Q6CAwDQYJKoZIhvcNAQEBBQAEggEAfDPAC0gM
# 28MTxM5uNXZO2oXjUKzxB9/KJHCLd1Cl0qWtS46aksimWnB8deJeNer2q0IoL97e
# qMlCaiKSiRlW4eY655a4A0U3Raj964DNPLPN87XPjb2PU06EJaAAjeLKb5jzaRV0
# OHSxAr5yh/w7xNqQVi3cVQHjgMcYEViejAtgT375T/MHLUlHRXXnB49Q14aswv14
# o5UFWIV1kji6wY2Kr+fJGIyCb2/bFFANyJwWhFqgMabSdW34nxars2IIqMk4v1+s
# IJ2rwkLieMD2XsKpjEfwOXs3cCS/dUJnbRtLA+uCNPvb5cv5iwvVXzYiPcz6fy5S
# GYvlNzYvV5PY6Q==
# SIG # End signature block

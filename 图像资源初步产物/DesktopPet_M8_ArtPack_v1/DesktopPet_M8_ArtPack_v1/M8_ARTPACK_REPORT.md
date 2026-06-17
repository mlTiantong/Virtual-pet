# M8_ARTPACK_REPORT

## Summary

- Scope: art resources only; no WPF code changes.
- Source green images: 10
- Keyed transparent sources: 10
- Animation entries: 29
- Props: 6
- Output PNG canvas: 900 x 900.
- Upright character bottom line: y=865.

## Important note

This pack is a first consistency baseline. It fixes the major color/scale inconsistency by using a single canonical master reference. Feed motion is scaffolded for multi-step implementation; a future art pass can replace aliased sequence frames with unique intermediate drawings.

## Package layout

```text
source_green_m8/                 # green-screen source images
assets/keyed_sources_m8/         # keyed transparent canonical source images
assets/animations_m8/            # animation/action folders
assets/props_m8/                 # transparent prop/fx assets
asset-manifest.m8.json
animation-manifest.m8.json
prop-manifest.m8.json
motion-sequence.m8.json
```
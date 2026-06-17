# CHARACTER_STYLE_LOCK — M8

This document freezes the desktop pet character into a single visual baseline.
All future art assets MUST reference these masters for consistency.

## Locked character identity

- Character: petite chibi anime desktop pet girl.
- Hair: very long icy blue / silver-blue hair, flowing to lower body, with black X hairpins and a blue side bow.
- Eyes: large saturated blue eyes.
- Outfit: ice-blue frilly dress with white lace trim, blue neck bow, white thigh-high socks, light-blue shoes.
- Palette: keep dress in pale ice blue; avoid purple/pink drift.
- Canvas: 900 x 900 transparent PNG after keying.
- Feet baseline: y = 865 for upright poses.
- Target character height: approximately 830 px for upright poses.

## Character master baseline images

All masters are located in `assets/character_masters/` (900 x 900, transparent PNG).

| File | Pose / Expression | Source (keyed_sources_m8) |
|------|-------------------|--------------------------|
| `character_master_idle.png` | Standard front-facing standing | `character_master_idle.png` |
| `character_master_blink.png` | Eyes closed / blinking | `idle_blink.png` |
| `character_master_smile.png` | Happy / cheerful | `idle_cheer.png` |
| `character_master_shy.png` | Shy / timid | `hover_shy.png` |
| `character_master_pout.png` | Pouting / angry cheeks | `face_pout.png` |
| `character_master_reach.png` | Reaching / inviting hand | `hand_invite.png` |
| `character_master_read.png` | Studying / reading a book | `study_guard.png` |

## Generation constraints for future assets

1. Use `character_master_idle` as the primary reference.
2. Keep full body visible; do not crop hair, hands, skirt, legs, socks, or shoes.
3. Use the same dress hue and same shoe/sock design.
4. Prefer green-screen source images for easy keying.
5. When creating motion sequences, produce multiple staged poses instead of one static final pose.
6. **Forbidden**: random outfit changes, color swaps, shoe/sock redesign, or head-to-body ratio changes.

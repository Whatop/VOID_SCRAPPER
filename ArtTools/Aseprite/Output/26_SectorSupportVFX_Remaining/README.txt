SECTOR ADMINISTRATOR - REMAINING SUPPORT VFX

Art-only handoff, authored/exported through Aseprite CLI + UTF-8-no-BOM Lua.
Five support roles plus a matching optional ring corner. No approved boss,
purple laser, core-ejection flash, missile body or static barrier is generated
or overwritten. This set does not change Unity code, prefabs or timings.

FAMILY                                  CELL     FRAMES / TAGS
VFX_Sector_EnemyMissileTrail              32x16    4 / Loop
VFX_Sector_RectangleRelease              32x16    6 / Warning, Release
VFX_Sector_RectangleCorner               16x16    6 / Warning, Release
VFX_Sector_BarrierFormationFront         32x16    4 / Travel
VFX_Sector_BarrierActivationSpark        16x16    4 / Activate
VFX_Sector_BarrierStabilizationPulse     32x16    4 / Connect

The formation front is reduced from the previous 32x32 source to 32x16.
The other five families preserve every existing source-layer pixel and timing.
All six have four editable layers, binary alpha, a maximum of four opaque
colors per frame, and integer 2px construction. No separate charge node is
needed: the first activation-spark frame already supplies a compact charge.

UNITY IMPORT
PPU 32; Sprite (Multiple); grid by the cell size above; Point filtering;
Compression None; mipmaps off; Full Rect meshes; no trimming. Each JSON file
contains exact frame rectangles, durations, tag ranges and normalized pivot.
All full strips proceed left to right. Keep the transparent cleanup cells.

MISSILE TRAIL
240ms loop, 4 x 60ms. Local +X is forward; exhaust points toward -X.
Pivot (28,8) from the image top-left, Unity (0.875,0.5), attaches at the
existing missile's tail. Rotate with actual heading, not the target vector.
Tail footprint is at most 24px long and 6px thick, with a 2x2 white center.
Keep the existing approved body/hostile outline. Sort propulsion below the
body and boss so six tails do not cover the core during initial deployment.
Reset the loop and clear any retained trail history when returning to a pool.
For curved pursuit, follow the projectile's actual sampled heading/path;
do not stretch one rigid tail back to the launch point or leave persistent
trajectory marks. The supplied effect is propulsion only, not guidance code.

ALTERNATING RECTANGULAR RINGS
The full sequence is 100/40/50/60/60/30ms. Warning is a single seed frame;
hold it for the authoritative gameplay warning. Release is a separate
five-cell PNG and tag totaling 240ms. Never use this art to decide damage.
Tile the 32x16 horizontal strip along straight edges (16px repeat).
Rotate by 90 degrees for vertical edges. Prefer tiling over length scaling
so pixels keep their thickness when arena dimensions vary.
Use the optional 16x16 corner at the top-left; mirror X/Y for the other
corners. Reserve those corner areas and tile the straights between them.
Do not overlap corners with strips or stack two edge effects at each bend.
Every active edge/corner shares one release clock. Play Release on the
damaging parity only; the opposite safe band receives no explosion effect.
The white release rails cover at most half of each tile; the band center
and rectangle interior stay transparent. The corner is an exact miter of
the strip, not an additional brighter burst.

CONTAINMENT FORMATION
Front: 4 x 80ms loop, local +X travel, pivot (24,8) / Unity (0.75,0.5).
Activation: 50/50/60/40ms one-shot, center pivot (8,8).
Stabilization: 40/60/80/40ms one-shot, pivot (0,8), 16px X repeat.
At the existing four corners, play Activation, then move a Front at each
growing boundary endpoint, rotating for that edge. Reveal the approved
static wall behind it. At connection, remove Front and play Connect once.
The compact front and pulse are already narrow: keep their intended world
scale rather than inheriting another barrier-thickness reduction. Apply
parent-scale compensation as needed. Never replace the approved static wall.
No corner-device art, permanent wall art or combat flash is included.

REVIEW FILES
SectorRemainingVFX_ContactSheet.png: all remaining families, 2x pixel review.
Individual *_Preview.png files: complete per-family frames, integer 3x.
SectorRemainingVFX_480x270.gif: native 6.6-second animated art review:
six core-origin outward missiles into broad pursuit; alternating ring
releases with the player on the safe band; four progressive boundary edges,
connection and stabilization. Corresponding native PNG checkpoints included.
The approved boss, player, missile body and static wall are references only.
Missile body is point-sampled to 20x10 for the review; no body sprite exported.
Static wall in the review uses the existing narrow .6 presentation scale.
Motion and safe-band diagrams illustrate the requested design, not a capture
or timing validation of the current Unity gameplay implementation.

VALIDATION / REPRODUCTION
validation.json: reopened Aseprite/PNG agreement, palettes, binary alpha,
layer reuse, duration/tag/pivot data, tile seams, corners, loops and cleanup.
readability_review.json: six in-frame pursuit missiles, safe-band/player
overlap checks and four simultaneously growing boundary fronts.
reference_hashes.json: all 670 protected Input / approved Output / selected
production references, compared before and after generation.
No approved core ejection or laser is included as a generated family.

Run Scripts/Generate-SectorRemainingVFX.ps1 from the established Aseprite
tool folder. It re-inspects exact references, verifies hashes, makes copies,
and runs the saved Lua generator and validator. -ReplaceGenerated can rebuild
only this generator's recognized 26_SectorSupportVFX_Remaining folder.
Runtime Unity integration and in-engine acceptance remain outside this handoff.

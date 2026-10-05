SECTOR ADMINISTRATOR - PHASE 2 LASER RELAY PACK

ONE REUSABLE RELAY
Sector_LaserRelay.aseprite contains Green and Purple single-frame state tags.
Both states use one shared white/light-gray chassis, silhouette, orientation
and attachment layout. Only 140 powered pixels differ in the flattened states.
Relay Chassis / Emitter / Energy layers remain separately editable.
The two added units use the same Purple state as the four existing units.
No alternate unit design, extra boss sprite or new full beam is included.

The inspected live LaserManagerShip.prefab uses the approved GreenSupport
texture, which matches Output/04_SystemSupport/system_support_green.png.
Its 64px canvas at 42.666668 PPU occupies 1.5 world units. The new 48px canvas
at PPU 32 matches those extents. Symmetry-aware point sampling preserves its
established construction without filtering. A small connection socket and
white-hot emitter center are added to the powered layers. The Phase 2 ramp
matches the approved Sector Administrator Purple Overdrive energy palette.

CONTENTS
Family                           Cell     Frames / Tags                 Timing
Sector_LaserRelay                48x48    2 / Green, Purple              Static
VFX_Sector_RelayDeploy           48x48    6 / Deploy                     310ms
VFX_Sector_RelayDisconnect       16x16    5 / Disconnect                 240ms
VFX_Sector_RelayReconnect        32x16    5 / Reconnect                  230ms
VFX_Sector_RelayStabilize        16x16    6 / Green, Purple              130ms each

Every family has an editable Aseprite source, transparent PNG sheet, frame
metadata and individual review sheets. Unit states and stabilization variants
also have separate Green/Purple PNG exports. Animated clips end transparent.
The relay uses 12 opaque colors; each VFX frame has at most six. All gameplay
exports use binary alpha, hard pixel edges and no filtering or soft glow.

UNITY IMPORT
PPU 32; Point filtering; Compression None; mipmaps off; Full Rect meshes.
Slice by each cell size above; preserve the transparent padding and cleanup
frames. JSON files specify every source rectangle, tag range, duration and
pivot. The relay state sheet is one column by two rows. Stabilization uses
three columns by two rows. Other VFX are horizontal strips.
Select Green or Purple explicitly; do not autoplay the two static relay tags.

PIVOTS AND DIRECTION
Relay: (24,24) from image top-left / Unity (0.5,0.5); artwork faces north.
Emitter origin: (24,24). The small socket light is at (24,13).
Deploy: (24,24) / Unity (0.5,0.5), centered on the arriving relay.
Disconnect: (4,8) / Unity (0.25,0.5), local +X toward the departing link.
Reconnect: (8,8) / Unity (0.25,0.5), local +X toward the rebuilding link.
Stabilize: (8,8) / Unity (0.5,0.5), centered on the emitter.
Use the existing gameplay-owned endpoint/rotation. These pixel anchors do not
replace serialized laser anchors, collision geometry or gameplay authority.
Do not inherit a whole-sprite phase tint: white armor must stay white.

LOCAL EFFECT HANDOFFS
Deploy durations: 50/40/50/60/70/40ms.
Marker -> compact cyan-white flash -> contour scan -> violet settling -> clear.
Reveal the independent relay sprite at 90ms (frame 3), beneath the VFX.
No chassis is baked into the deploy sheet. The scan follows the relay contour;
the visible unit remains the same reusable Green/Purple asset.

Disconnect durations: 40/50/50/60/40ms.
Trigger at the local connection endpoint while the existing beam shuts down.
Bright tip -> lower-intensity green -> short separated tip -> final spark.
This 16x16 effect is not a full laser and must not be stretched across a lane.

Reconnect durations: 50/50/40/60/30ms.
Charge -> white-hot socket -> short outward discharge -> settle -> clear.
Suggested visual handoff to the approved Purple Laser is 140ms, when the
discharge begins settling. The main beam remains the existing approved asset:
Output/24_SectorPurpleLaserVFX/VFX_Sector_PurpleLaserBeam.aseprite.
Gameplay warning/damage timing remains controlled by the existing owner.

Stabilization: 40/50/40ms, same small geometry in Green and Purple.
Play once on arrival/final placement. Clear active clips when returning pooled
units to the pool. No persistent particle systems or additional charge node
are required by this pack.

FORMATION AND NATIVE REVIEWS
SectorRelay_GreenPurple_Comparison.png compares the identical chassis at
integer 3x and native 48x48. SectorRelay_ContactSheet.png shows every frame.
SectorRelay_FormationComparison_Review.png tracks the four original positions
and the six-unit Phase 2 layout. IDs match the inspected existing owner:
1 bottom-left, 2 top-left, 3 top-right, 4 bottom-right; 5 new top, 6 new bottom.
The four existing units move to the regular-hexagon shoulders, while 5 and 6
occupy the top/bottom points. All six use the same relay design.
SectorRelay_SixUnitFormation_Review.png is an exact integer 2x review image.

SectorRelay_480x270.png and SectorRelay_480x270.gif are native-size art reviews.
The four-second GIF shows green links shutting down, two units materializing,
all six becoming Purple Overdrive, local reconnect flashes, rebuilt links and
rotating-laser readability. Separate disconnect/deploy/reconnect/rotation
PNG checkpoints are also included. Approved boss sprites and full beam art
appear only as untouched references. Preview beams are point-sampled at half
their original transverse thickness, with no new beam export.

These are illustrative pixel-art compositions, not Unity Play Mode captures.
Positions, speeds and transition timings illustrate the requested presentation;
they do not change or validate runtime logic. Formation reviews are opaque
reference images and must not be imported as a single gameplay sprite.

VALIDATION AND REPRODUCTION
validation.json records 24 frames / 7 tags, PNG-versus-Aseprite equality,
transparent cleanup, limited palettes, binary alpha, metadata/timing/pivots,
source-derived chassis, bilateral symmetry, identical Green/Purple alpha masks,
unchanged chassis pixels and the scope of the powered-color changes.
reference_hashes.json records 713 protected references checked before and after
generation, including all approved Output sets through 26 and all Input files.
Approved Sector Administrator sprites and Purple Laser sources are unchanged.

Scripts/Generate-SectorRelayPack.ps1 runs the UTF-8-without-BOM Lua generator
and validator against copied, hash-verified references. The shared pixel helper
is reused unchanged. -ReplaceGenerated rebuilds only this generator's recognized
27_SectorLaserRelay output folder. Existing approved art is never overwritten.
This handoff contains art and reproducible scripts only; Unity integration is
not performed. Existing prefabs, .meta files, code and saved scenes are untouched.

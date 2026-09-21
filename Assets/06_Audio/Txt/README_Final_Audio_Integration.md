# VOID SCRAPPER - Final Audio Integration

Completed against the local working tree on 2026-09-19. No commit or push.

The library went from 105 to 102 events: 12 previously empty events filled,
8 events with broken clip references restored, 4 unused events retired, and
remote communication event 111 added. Only Settlement ambience remains empty.

## Source and inspection

- Source: `Assets/06_Audio/Sound.zip`, 170,686,286 bytes. Original archive preserved.
- SHA-256: `b1d961ecc96b4da6cb11c2c7ea07c3465e7c455c18c2edf5e272006ca89a2b3f`.
- Outer archive: 214 entries (206 files, 8 directories), including 139 audio files,
  64 archived `.meta` files, one nested ZIP, one shortcut and one `.gitkeep`.
- Nested `분류필요.zip`: 37 audio files, all byte-identical to outer-archive files.
  Those duplicate files were indexed without extracting duplicate copies.
- Combined inventory: 243 file records, 176 audio records, 131 unique audio hashes.
- Extracted under `Assets/06_Audio/ImportedSource/SoundZip~/`. The trailing `~`
  keeps raw candidates, archived GUIDs and compilations out of Unity import.
  Archive paths were validated before extraction. No shortcut was executed.
- Inspection used FFprobe format/duration/channel/tags, SHA-256 provenance,
  FFmpeg silence/amplitude analysis and existing project mapping CSVs.
  **No clips were auditioned:** this environment explicitly rejected audio input.
  Metadata supports the selected uses; perceived timbre, loudness and timing
  still require the listening checks below.
- No external audio was searched for or downloaded. No waveform was sliced,
  processed or synthesized. No BGM was changed.

The complete per-file classification, original names, extracted locations,
durations, formats, hashes and selection reasons are in
[Final_Audio_SoundZip_Classification.csv](Final_Audio_SoundZip_Classification.csv).

## Compilations for Cakewalk Sonar

All are under `Assets/06_Audio/ImportedSource/SoundZip~/Compilations/`.
None is assigned to a SoundEvent. Manually cut and audition them in Cakewalk Sonar.

| Extracted filename | Seconds | Original/source name | Decision |
| --- | ---: | --- | --- |
| `모음집1.flac` | 15.273 | Same outer name; nested `___menu-sounds.flac` | Preserve existing compilation name |
| `모음집2.wav` | 76.129 | Same outer name; nested `laser-effect-contact-mic.wav` | Preserve existing compilation name |
| `모음집3.wav` | 6.021 | Same outer name; nested `coins-game-sound.wav` | Preserve existing compilation name |
| `모음집4.ogg` | 70.128 | Same outer name; nested `ui.ogg` | Preserve existing compilation name |
| `모음집5.wav` | 23.095 | `Rock/Rock_Impact.wav` | Renamed extracted copy; multiple impacts separated by silence |

The rock compilation also contains floating-point samples above 0 dBFS; check
levels when cutting. Pre-split UI/credit candidates in the archive were retained
as candidates; this pass did not generate those cuts.

## Library audit and exact assignments

[Final_Audio_Event_Audit.csv](Final_Audio_Event_Audit.csv) lists **every** original
event ID, its final ID/status, previous clip-slot/missing-reference count, every
final AudioClip path, and final volume, pitch, loop, priority, spatial, distance,
voice-limit and retrigger settings. Rows for retired IDs remain in the report.

The actual pre-pass empty entries were:

```text
13_ui_settings
16_shop_open
18_shop_buy_fail
19_shop_item_sold
20_shop_transaction_complete
22_shop_hostile
37_pickup_scrap
38_pickup_core
40_pickup_experience
41_level_up
49_ship_dash_end
83_return_beacon_spawn
84_wormhole_enter
95_amb_settlement_loop
107_map_route_placed
108_map_route_removed
109_mission_received
```

Paths below are relative to `Assets/06_Audio/SFX/`. Existing clips are referenced
directly, without duplicating files. Event 40 reuses one restored archive clip.

| Final event | AudioClip path | Selection/reuse |
| --- | --- | --- |
| `13_ui_settings` | `UI/ui_confirm_01.wav` | Same AudioClip as `04_ui_panel_open`; volume 0.55 |
| `16_shop_open` | `Shop/shop_open.wav` | Existing shop category/open cue, 0.465 s |
| `18_shop_buy_fail` | `Shop/shop_buy_fail.wav` | Existing insufficient-currency cue, 0.458 s |
| `22_shop_hostile` | `Shop/shop_hostile_03.ogg` | Existing Kenney low-three-tone warning; no extra hostile communication variant |
| `37_pickup_scrap` | `Pickups/pickup_scrap_03.ogg` | Existing short tin impact, 0.215 s; volume 0.38 |
| `38_pickup_core` | `Pickups/pickup_core.wav` | Existing achievement/acquisition cue, 1.456 s |
| `40_pickup_tuning_chip` | `UI/reinforcement_equip.wav` | Restored item-equip confirmation; reused at 0.48 volume, pitch 1.03-1.06 |
| `83_return_beacon_spawn` | `Return/return_beacon_spawn_03.ogg` | Existing force-field energy cue, 0.953 s |
| `84_wormhole_enter` | `Return/wormhole_enter.ogg` | Existing phase-jump cue, 0.470 s |
| `107_map_route_placed` | `Events/event_start.wav` | Existing objective-reached confirmation, 0.359 s; volume 0.38 |
| `108_map_route_removed` | `UI/tab_status_close.wav` | Restored inventory-close cue, 0.244 s; volume 0.30 |
| `109_mission_received` | `Events/event_start.wav` | Same objective confirmation as route placement; separate ID/volume/retrigger |
| `111_dialogue_comm_incoming` (new) | `Radar/radar_scan_pulse.wav` | Existing social/communication notification, 0.493 s; distinct from current radar scan assignments |

UI, pickup, map, mission and communication assignments are explicitly Force2D.
Shop hostility, beacon arrival and wormhole entry retain the world-SFX contract:
World2D, blend 0.25, linear falloff, no Doppler and project occlusion defaults.
Voice limits and retrigger intervals differ by event. Communication is priority
32, one voice, one-second minimum retrigger and fixed pitch. All new one-shots
are non-looping. Full configuration is in the audit CSV.

## Restored broken references and new audio assets

Eight pre-pass entries contained 21 GUID references with no corresponding
importable project asset. Their original GUIDs match archived `.meta` files in
`Sound.zip/UI/예전거 임시분리/`. Twenty associated audio files were copied into
the established `SFX/UI/` folder.
The remaining clip was byte-identical to existing `SFX/Pickups/pickup_scrap.wav`,
so event 26 reuses that existing asset without a duplicate file. Unity generated
**new real GUIDs**; archived GUIDs were not copied or fabricated. Existing event
configurations were retained.

| Event | New files under `Assets/06_Audio/SFX/UI/` |
| --- | --- |
| `23_trait_select` | `trait_select.wav`, `trait_select_02.wav`, `trait_select_03.wav` |
| `24_reinforcement_equip` | `reinforcement_equip.wav`, `reinforcement_equip_02.wav`, `reinforcement_equip_03.wav` |
| `25_reinforcement_drop` | `reinforcement_drop.wav`, `reinforcement_drop_02.wav`, `reinforcement_drop_03.wav` |
| `26_reinforcement_pickup` | `reinforcement_pickup.wav`, `reinforcement_pickup_02.wav`; third clip reuses existing `../Pickups/pickup_scrap.wav` |
| `28_tab_status_open` | `tab_status_open.wav`, `tab_status_open_02.wav` |
| `29_tab_status_close` | `tab_status_close.wav`, `tab_status_close_02.wav` |
| `30_warning_message` | `warning_message.wav`, `warning_message_02.wav` |
| `31_action_denied` | `action_denied.wav`, `action_denied_02.wav`, `action_denied_03.wav` |

Each of these 20 new WAVs has a Unity-created `.meta`. Only their import settings were
configured: DecompressOnLoad, Vorbis quality 1, preserved sample rate, preloaded,
no background loading. Existing clips' import settings were not changed.

## Retirements and unchanged authorities

- `40_pickup_experience` became `40_pickup_tuning_chip`. `PickupTuningChip` is a
  primary constant with `pickup_tuning_chip` legacy mapping. `PickupExperience`
  and its legacy mapping are gone. Existing unused XP-named source WAVs were not
  deleted; no event references them.
- `41_level_up` and `LevelUp` were removed. No valid gameplay audio caller remained.
  Pixel Curse progression/localization and reward gameplay were not changed.
- `49_ship_dash_end`, its constant/mapping, AudioManager classification and the
  PlayerDash runtime call were removed. The existing 0.160-second dash-start
  clip has a measured fading tail; no clearly useful release source was verified.
- `19_shop_item_sold` and `20_shop_transaction_complete` were removed after
  the project-wide reference search found only declarations/classification/library
  data. The current shop has no player-sale operation; event 17 already confirms
  successful transactions. No new selling behavior or redundant confirmation
  was invented. All four retired numbered slots remain gaps.
- All retained IDs 42+ are unchanged, including the 99-to-105 jump. Local Git
  history contained no prior `111_` declaration in SoundEventIds.
- ShopTradeUI still owns first-open 16, failed-purchase 18 and post-success 17.
  Its transaction buttons already disable generic click audio. ShopStructure
  retains both authoritative hostility paths for 22; its retrigger guard limits
  clustered hostility notifications. No shop gameplay code was changed.
- RewardPickup retains its single post-grant dispatch for Credit, Scrap, Core,
  Tuning Chip and Heal; Stabilized Alloy uses Scrap. RewardCapsule's direct NPC
  reward still grants Tuning Chips before playing event 40. No generic pickup
  sound was added. Region2BossCoreRewardPresentation retains its Core cue.
- ReturnBeacon's existing spawn call and WormholePortal's existing entry call
  are unchanged. Neither waits for clip completion or delays a transition.
- Map route placement/removal and mission assignment callers are unchanged.

## Explicit remote communication and tutorial relay

`111_dialogue_comm_incoming` uses the existing AudioManager. There is no global
dialogue beep and no new audio manager. Pixel Crushers sends conversationStarted
after accepting a conversation, before the first subtitle. Both hooks subscribe
only around their own start attempt and unsubscribe in `finally`; failed starts
and subtitles do not produce a cue. The existing AudioManager retrigger guard
limits rapid interrupted retries. The cue remains allowed during world-SFX
suppression so remote story communication can accompany presentation locks.

- `DialogueStoryEntryPoint.HandleIncomingCommunicationStarted` (line 171):
  serialized `playIncomingCommunicationCue`, default false. The existing NULL
  DISPATCHER prefab opts in (prefab line 337); its shared entry covers treatment
  network contact and the ending conversation. Boss combat/progression code and
  generated dialogue content were not changed.
- `TutorialFlowController.StartRemoteConversation` (line 4480): reuses the
  existing incoming-transmission event field, with 111 as the default for old
  scenes whose field is empty. Explicit callers are relay analysis/fake takeover
  (line 967), Operator guidance (2095), unknown-access-key Operator response
  (3496), and the Settlement rescue transmission trigger (4467). The latter is
  a remote tutorial transmission, not FieldNpcObjective's nearby RescueContact.
- FieldNpcObjective's direct NPC/RescueContact start path is unchanged.
  DialoguePixelCrushersBridge has no audio hook. Local Settlement story entry
  remains default false. No actor-name or visible-text inference was introduced.
- Relay inspection found only a visual pulse plus PlayerInteractor's generic
  click; MissionReceived plays later at the unknown-objective handoff. The
  accepted relay interaction now calls existing `EventStart` at line 866 with
  scale 0.65, inside the existing one-shot analysis guard. It does not fire from
  Update, radar scans or fake-takeover visual pulses. No new relay event was added.
- MissionReceived stays `109_mission_received`, separate from communication 111.

## Unresolved and audition-only candidates

**Settlement ambience still needs a dedicated loop.** Event 95 remains empty;
GameAudioLoopController's existing Settlement StopAmbience behavior is unchanged.
The existing `Ambience/amb_settlement_loop_01.wav` is 3.998 seconds with Sci-Fi
metadata and a continuous measured envelope. That cannot establish its audible
identity or a clean perceptual loop. It remains unassigned pending audition.
No BGM track was duplicated into ambience.

- Archive `pickup_tuning_chip.wav` equals nested `eating_chip.wav`: unassigned.
  Listen before considering it for a chip/data pickup.
- `event_signal_activate.mp3` equals generic `eventsounds.mp3` (0.866 s): possible
  relay/system candidate after audition, not established as a receive chirp.
- `event_transition_woosh.wav` (5.275 s) is long for the requested wormhole one-shot;
  the existing 0.470-second phase-jump clip was retained instead.
- `ui_retro_confirm.wav` and `event_tutorial_anomaly.wav` need sound-level review;
  names alone do not establish a clean single event.
- `curse_alien_glitch.wav` / `sfx_glitch_hit.mp3` remain unassigned presentation
  candidates. Neither is verified as an exceptional hostile communication cue;
  no hostile variant event was added.
- Remaining pre-split UI/credit and combat candidates are listed in the
  classification CSV; none was claimed to have been auditioned.

## Files and validation

Modified existing files:

- `02_Scripts/Audio/SoundEventIds.cs`
- `02_Scripts/Audio/AudioManager.cs`
- `02_Scripts/Player/PlayerDash.cs`
- `02_Scripts/Dialogue/DialogueStoryEntryPoint.cs`
- `02_Scripts/Tutorial/TutorialFlowController.cs`
- `02_Scripts/Dialogue/Editor/Tests/QAStabilizationPass1Tests.cs`
- `03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab` (one opt-in field only)
- `06_Audio/Resources/Audio/SoundEventLibrary.asset`

Added: `02_Scripts/Audio/Editor/FinalAudioIntegrationInstaller.cs` and its Unity
`.meta`, the 20 WAV/.meta pairs above, the separated imported-source folder,
this README and the two audit CSVs (with Unity-created metadata). The installer
uses AssetDatabase/SerializedObject/PrefabUtility and can be run from
`VOID SCRAPPER > Audio > Apply Final Audio Integration`.

Actually executed:

- Runtime static compilation: **0 errors, 52 warnings**.
- Editor/test static compilation: **0 errors, 4 warnings**. The first harness
  attempt referenced a stale runtime DLL; correcting the response-file reference
  to the newly compiled runtime assembly resolved that tooling-only error.
- Unity 6000.0.69f1 batch import/authoring: succeeded, exit 0.
- Audio-focused EditMode tests, including existing shop regression: **21/21 passed**.
- Full EditMode suite: **456/456 passed**, 0 failed, 0 skipped, 0 inconclusive.
- Serialized library checks: 102 unique known IDs, no missing assigned clips,
  no compilation assignments, all primary mappings covered, correct event-40
  migration, no renumbering, panel-open reuse and one unresolved ambience entry.
- Scoped hand-authored `git diff --check`: passed. Unity serialization was not
  manually reformatted. Comparison against the 10,236-file pre-pass asset snapshot
  found exactly the eight intended existing-file changes above, no unrelated
  changes and no missing files. The original archive hash is unchanged.

Execution logs, static compiler response files and NUnit XML are in
`%TEMP%/void-final-audio/` (`unity-final-import.log`, `audio-tests-final.xml`,
`full-editmode-final.xml`, `Assembly-CSharp-static.log`, `Assembly-CSharp-Editor-static.log`).
No Play Mode campaign or listening QA was executed.

## Exact remaining Unity manual checks

1. Start from Boot and play the Tutorial. Listen to opening/guidance calls,
   relay activation, real/fake Operator handoff, unknown objective/Core response,
   and the rescue transmission. Confirm one incoming cue per accepted start,
   no rapid retry chatter and no cue per subtitle.
2. Interact with a nearby RescueContact and local Settlement speaker: no incoming
   communication cue. Test NULL DISPATCHER treatment and ending network contact,
   including interruption/retry, without changing campaign completion behavior.
3. Open the shop, fail a purchase, complete a purchase and provoke hostility.
   Listen for the dedicated confirmations and absence of generic transaction
   click stacking; check hostility against the simultaneous shield-break sound.
4. Collect Credits, clustered Scrap, Core, Heal and Tuning Chips; also take the
   RewardCapsule chip reward. Verify distinctions, volume and rate limiting.
5. Repeatedly dash: start/tail only. Place/remove/clear map routes, receive a
   mission and open Settings. Check readability and conservative volume.
6. Spawn the return beacon and enter a wormhole from near/far camera positions.
   Check world attenuation, transition timing and whether scene loading clips
   the short whoosh in the actual campaign flow.
7. Audition all changed/restored clips at gameplay mix levels. Cut the five
   compilations in Cakewalk Sonar only if needed. Audition the unresolved
   Settlement ambience candidate for identity and several repeat seams before
   binding it and enabling the existing ambience channel for Settlement.

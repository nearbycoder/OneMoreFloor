# One More Floor: improvement plan (round after v0.1.0)

Written 2026-10-06 on the `improvements` branch, after a baseline pass over the released v0.1.0 code. Everything
below is grounded in what the checks, the simulation harness and the built game's screenshots show. Nothing here
has been implemented yet.

## Baseline (v0.1.0 at `035c30f`)

| Check | Result |
| --- | --- |
| `Tools/sim.sh fuzz` | **PASS** (random commands against every shift, invariants held) |
| `Tools/unity.sh test` (EditMode) | **PASS**: 16/16 |
| `Tools/unity.sh build-linux` | **PASS**: 143 MB, 0 errors |
| `Tools/autopilot.sh` (built game, all ten shifts, then the virtual-gamepad pass) | **PASS**: all ten shifts, 8/8 pad checks, no logged exceptions. 73 fps average on the Radeon 8060S iGPU; worst frame 247 ms (a load hitch) |
| `Tools/sim.sh human` / `causes` | Runs. The numbers are below, under "What the baseline shows". |

Not run this phase: `Tools/make_trailer.sh` (out of scope) and the audio audit, which nothing here touches.

An extra autopilot run of the Graveyard Shift in a 1440×900 window also passed, but it **did not test 16:10 layout**:
`Shots.Capture` always renders at 1920×1080, whatever the window size. 16:10 and 21:9 are unverified (see R3).

### What the baseline shows

**1. The week is a wall for new players, and Wednesday is a spike.** The human-model balance (`Tools/sim.sh human`,
16 seeds) gives the modelled *new* player (skill 0.15) a 1★ on 94% of Mondays but only **44–56% on every shift from
Tuesday on**. That's by design ("1★ = the median new player"), but each shift is a gate, and the README says the
model "plays smarter than a first-timer". A real newcomer probably fails more than half the time on most shifts, and
the next shift stays locked until they don't. `Tools/sim.sh causes 0.15` shows where it bites:

| Shift | New player fired | Main cause (mean per run) |
| --- | --- | --- |
| Tuesday | 0% | stormed off 0.75 |
| **Wednesday** | **37%** | **stormed off 2.69** |
| Thursday | 18% | stormed off 1.75 |
| Friday–Monday Again | 31% | stormed off ~2, swept away up to 1.7 |
| Graveyard | 18% | |

Wednesday has the **fastest spawn pacing of the whole week** (3.35 s → 1.7 s; Thursday on is 3.9 s+ → 2.05 s+)
while it introduces three things at once (Mirror Movers, Rise/Sink cards, a 7th floor). The `pace` search accepts
up to 40% of new players fired, so Wednesday passes it at 37%.

**2. Guests and floor labels are hard to read in the late shifts.** In the nine-floor shifts the tower fills about
550 of 1920 px of width, with empty sky on both sides. Floor nameplates are about 8 px tall, and guests are about
50 px. Departure signs ("LEAVING IN 2" on the floor's back wall) are about 6 px tall, even though couriers
depend on them. The camera framing reserves about 7 world units for the street and roof. The zoom tip helps, but only once,
and the close-up loses the overview that routing depends on.

**3. A timer game that keeps running when you look away.** `runInBackground: 1` is set, and nothing listens for
`OnApplicationFocus`. Alt-tabbing, a notification stealing focus, or a controller dropping out mid-shift lets
patience drain and complaints pile up. Nothing pauses on a gamepad disconnect either.

**4. Failure doesn't teach much.** The time card shows delivered, best streak, complaints and stops, and "Next
star at $X". The sim already records why each guest was lost (`Outcome`: stormed off, fumed in the car, poofed,
parcel lost, swept away, went with the floor), and `Telemetry` logs the counts, but the player never sees that breakdown or a hint about it.

**5. Platform reach.** The blog lists Windows · macOS · Linux, but the release is Linux-only. This editor has
`MacStandaloneSupport` and `WebGLSupport` but no Windows module. Release packaging is manual: there is no script
that produces the zip and `.sha256` in `Builds/Release`.

**6. Small things seen along the way:**
- The tower plaque says **"EST. 1931"** (`BuildingView.cs:116`), and the title screen and trailer say "EST. 1929".
- On Monday, the coach ring sits on top of the forecast text it points at ("Lo◎by", "The Bui◎ding").
- `PLAN.md` lists a **reduced-motion** setting, but Settings only has shake, fullscreen, forecast and volumes.
- Gamepad prompts are always A/B/X/Y (the README says so too). On a DualShock or DualSense they're wrong, and on
  Switch-layout pads A and B are physically swapped.
- Windowed mode is a fixed 1600×900 (`GameRoot.ApplySettings`). On a 1366×768 or 1280×800 screen that's larger
  than the display.

## Ranked improvements

Impact is for a real player. Effort: S < 1 day, M 1–2 days, L more. Risk is the chance of breaking something or
of needing a judgment call that only playtests can settle.

| # | Improvement | Impact | Effort | Risk |
| --- | --- | --- | --- | --- |
| 1 | **Unlock pacing and the Wednesday spike:** retune Wednesday's spawn pacing, tighten the `pace` target for new players, and add a "late pass" so a shift unlocks after a few honest attempts even without a star | High: decides whether newcomers see past Wednesday | M | Medium: balance, checked against the model, not people |
| 2 | **Auto-pause on focus loss and on controller disconnect** | High: prevents unfair losses | S | Low |
| 3 | **Readable late shifts:** tighter in-play framing that trims street and roof margins, and screen-space floor labels (slot number, icon, name, waiting count) in the empty gutter beside the tower | High: every shift from Thursday on | M | Medium: layout at different aspect ratios |
| 4 | **Time card that teaches:** complaints broken down by cause with a one-line tip for the biggest one, and "$X short of the next star" | Medium-high: turns a failed shift into a plan | S–M | Low |
| 5 | **macOS build and a release script:** a universal (x64 + arm64) `.app` zip next to the Linux zip, with `.sha256` files, from one command | Medium-high: platform reach the blog already promises | S–M | Medium: can't be run or notarized here |
| 6 | **Controller glyphs per family** (Xbox / PlayStation / Switch layout) in the prompt strip and coach tips | Medium for pad players | S | Low |
| 7 | **Reduced-motion setting** (no idle drift, push-ins or Flip aberration pulse; shorter floor lurches), as `PLAN.md` intended | Medium (accessibility) | S | Low |
| 8 | **Display sanity:** clamp the windowed size to the display, and check the HUD and panel at 16:10 (every Mac) and 21:9 | Medium (matters more once a Mac build exists) | S | Low |
| 9 | Small fixes: the EST. year mismatch, and the coach ring covering the forecast text | Low | S | Low |
| 10 | **Daily Overtime:** a date-seeded Overtime with its own local best, so there's a reason to come back | Medium (replayability) | M | Low |
| 11 | **Relaxed mode:** a patience multiplier and no firing, with stars marked as relaxed | Medium (accessibility) | M | Medium: interacts with stars and unlocks |
| 12 | Larger guests at full zoom-out (a guest-size option, or re-proportioned floors) | Medium | L | High: touches the art layout of every floor |
| 13 | Windows build | High reach | S once the module is installed | **Blocked:** needs Windows Build Support installed in Unity Hub |
| 14 | WebGL build | Medium (instant play) | L | High: `MasterLimiter` uses `OnAudioFilterRead`, which WebGL doesn't support, and the sample-locked stem playback (`PlayScheduled` plus shared pitch warble) is unreliable there. Hosting it is also an outward publish. **Not a good fit now.** |
| 15 | An audio listening pass (variety, fatigue on the hundredth ding) | Medium | M | Needs human ears; can't be judged by measurement alone |
| 16 | A `LICENSE` | n/a | S | **Owner decision** |

## Proposed scope for this round

Six items: the five highest-impact ones plus a bundle of small fixes. Each item ends with the full check suite
(`sim.sh fuzz`, `unity.sh test`, `build-linux`, `autopilot.sh`) passing. The README's "Status and known issues"
is updated to match what was actually done and verified.

### R1. Unlock pacing and the Wednesday spike (#1)

- Retune Wednesday (and any other shift that misses the target) with `Tools/sim.sh pace`, using a tighter target:
  modelled new players (skill 0.15) are fired on **≤ 20%** of runs on every shift, and average players on ≤ 10%.
  Then regenerate the star thresholds.
- **Late pass:** after **three finished attempts** on a shift without a star, the next shift unlocks, with a sticky
  note from The Management ("We'll pretend we didn't see that."). Stars, bests and the ending are unchanged.
  Overtime still needs the Graveyard Shift cleared with a star.
- **Acceptance:** `sim.sh causes 0.15` shows new-player fired ≤ 20% on every shift, and Wednesday's 1★ rate for new
  players is at least Tuesday's. The roster explains the late pass on the locked card ("or 1 more try"). A new
  EditMode test covers the late-pass unlock rule and save round-trip.
- **Verify:** `sim.sh human` / `causes` / `pace` tables before and after, recorded in the commit message. The
  EditMode tests. A roster screenshot from the built game with a seeded save.

### R2. Auto-pause on focus loss and pad disconnect (#2)

- During a running shift, losing window focus or the active gamepad disconnecting opens the pause screen. Menus are
  unaffected. Automation (`-omfAutopilot`, demo and trailer) is exempt.
- **Acceptance:** with focus lost, the sim clock doesn't advance. Resuming continues from the same tick. Removing
  the active gamepad pauses with a "Controller disconnected" line.
- **Verify:** an autopilot step that calls the focus handler and removes the `PadSim` virtual device, then checks
  that the pause screen is visible and `Sim.Time` is frozen. Plus a manual alt-tab check in the built game on this
  machine.

### R3. Readable late shifts (#3, #8)

- In play, frame the tower more tightly (trim the street and roof margins, and let the tower use more of the height
  between the HUD and the panel). Add screen-space floor labels in the gutter on the left of the tower: slot
  number, floor icon, name, and a waiting-count pip, following shuffles. Readable at nine floors, and hidden in the
  close-up zoom where the in-world plates are big enough.
- Clamp the windowed size to about 90% of the display.
- **Acceptance:** at 1920×1080 with nine floors, floor names are at least 18 px tall and guests are at least 20%
  taller than at baseline. Nothing overlaps the HUD card, the panel or the prompt strip at 16:9, 16:10 (1440×900)
  and 21:9 (2560×1080). `Tools/uicheck.sh` still hits every control.
- **Verify:** autopilot screenshots of Graveyard at the three aspect ratios (`Shots.Capture` first has to learn to
  capture at the window's real size, since it's fixed at 1920×1080 today), compared side by side with the baseline
  captures, and pixel measurements taken on the images.

### R4. A time card that teaches (#4)

- Under the stats, list complaints by cause in plain words ("2 stormed off waiting · 1 vampire met the sun"), with
  one tip for the biggest cause, written in the same voice as the coach tips. Show "$X to ★★" next to the existing
  "Next star at". When nothing went wrong, keep the current line.
- **Acceptance:** each failing `Outcome` (StormedOff, Fumed, Poofed, SweptAway, PackageLost, WentWithFloor) has a
  label and a tip. The card's layout doesn't change for a clean shift.
- **Verify:** an EditMode test that every failing `Outcome` maps to text, and an autopilot results screenshot of a
  shift played by a deliberately weak bot (`Bot.Human(seed, 0)`) so complaints show up.

### R5. macOS build and a release script (#5)

- `BuildScript.BuildMac` (universal x64 + arm64 `.app`), plus `Tools/unity.sh build-mac` and a `Tools/release.sh`
  that builds Linux and macOS one at a time and writes `OneMoreFloor-v<version>-{linux-x86_64,macos-universal}.zip`
  and `.sha256` files into `Builds/Release`. The zip keeps symlinks and executable bits.
- **Acceptance:** the build succeeds. `file` shows a universal Mach-O binary with both architectures. `Info.plist`
  carries the version, the bundle id and the icon. The README gives download and run steps, including the
  right-click → Open / `xattr -cr` Gatekeeper step for an unsigned, un-notarized app, and states plainly that the
  macOS build has **not been run on a Mac**.
- **Verify:** build logs plus `file` / `plutil`-equivalent checks on the output. Nothing is uploaded: publishing
  the release stays with the owner.

### R6. Pad glyphs, reduced motion and small fixes (#6, #7, #9)

- Prompt and coach glyphs follow the active pad's family: Xbox (A/B/X/Y), PlayStation (✕/○/□/△) and Switch-style
  layouts. A **Reduced motion** toggle in Settings is saved. Fix the EST. year and the coach marker overlap.
- **Acceptance:** the autopilot's pad pass, run with a virtual `DualShock4GamepadHID`, shows ✕ in the prompt strip.
  Reduced motion persists and turns off the drift, push-in and aberration effects.
- **Verify:** autopilot screenshots of the prompt strip for both pad families, a settings screenshot, the
  `uicheck.sh` hit test, and a save round-trip test.

### Not in this round

Daily Overtime (#10) and Relaxed mode (#11) are good next candidates once R1 has settled the unlock rules. Larger
guests (#12) is an art-layout project of its own. Windows (#13) waits on the module, WebGL (#14) is a poor fit for
this audio design, and the listening pass (#15) and license (#16) need the owner.

## Decisions for the owner

1. **Windows build:** install *Windows Build Support (Mono)* for 6000.6.2f1 in Unity Hub if you want a Windows zip.
   It's an S-sized item once the module is installed.
2. **Late pass:** OK to unlock the next shift after three attempts without a star? The alternative is to keep
   1★ gating strictly and only retune the pacing.
3. **macOS distribution:** ship unsigned (with Gatekeeper instructions), or hold the Mac zip until it can be signed
   and notarized on a Mac with an Apple Developer account?
4. **License:** still unset ("all rights reserved").

## Outcome of this round (2026-10-06)

The orchestrator approved R1–R6 and the late pass. It decided: skip Windows (module not installed), build macOS
locally (unsigned, `com.nearbycoder.<game>` bundle id, not published), and leave the license, releases and signing
to the owner. Every item landed and was checked against its acceptance criteria, with one shortfall noted under R3.
Screenshots are in `docs/media/improvements/`. The final full autopilot on the last build, in a real 1920×1080 window, passed all ten shifts, 8 pad checks and 12 flow checks, at 80 fps average (worst frame 90 ms) on the Radeon 8060S iGPU.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| R1 pacing + late pass | `4192ea9` | `sim.sh causes/human 32`, 5 new EditMode tests, autopilot `ui` (third starless Tuesday opens Wednesday) | New-player fired on Wednesday 37% → 3%, worst shift now 18% (Thursday). Wednesday 1★ for new players 41%, equal to Tuesday. |
| R2 auto-pause | `c73bac5` | autopilot `ui`: focus callback and unplugging the virtual pad mid-shift | Met. A real alt-tab wasn't scripted (Wayland); the check drives Unity's own `OnApplicationFocus`. |
| R4 time card | `b1d6263` | 3 new EditMode tests (every cause has words and a tip; counts add up over bot runs of every shift), autopilot `ui` | Met. |
| R6 glyphs, reduced motion, fixes | `34c6815` | autopilot `ui` with virtual `DualShock4GamepadHID` / `SwitchProControllerHID`, settings toggle + save round trip, `uicheck.sh` | Met with virtual devices only. The Flip aberration pulse from PLAN.md never existed, so reduced motion covers drift, push-ins, shake, floor overshoot and UI wobble. |
| R5 macOS + release script | `b62706f` | build log, `file` (universal x86_64 + arm64 for the binary and every dylib), Info.plist, `sha256sum -c`, the extracted Linux zip passing the pad autopilot | Built and packaged locally only. **Never run on a Mac**, unsigned, un-notarized. |
| R3 readable late shifts | `3018ed9` | slab-pitch measurement on captures, autopilot at 1920×1080 / 1440×900 / 2560×1080, `uicheck.sh` | Guests +26% (mouse) / +22% (prompt strip). No overlaps at the three aspect ratios. **Label caps measure ~16 px, short of the 18 px target** (24 px type). |

Found along the way:
- `Tools/play.sh` always put `-screen-width 1600 -screen-height 900` before the caller's arguments, and Unity takes
  the first one. So every autopilot run, including the 0.1.0 baseline, ran in a 1600×900 window. Fixed in R3.
- A full autopilot run once crashed inside Wayland event dispatch (`wl_display_dispatch_queue_pending`, during
  startup resizes that every run makes). The rerun passed and it hasn't recurred. It looks like a Unity/libdecor
  issue on this compositor, not game code, but it's unexplained.
- `/tmp` here is a shared tmpfs that other sessions fill up, and one run failed on "Disk full". Automation output now
  goes to the repo's gitignored `Logs/`.

Still open: Daily Overtime (#10), Relaxed mode (#11), larger guests by art layout (#12), Windows (#13, needs the
module), WebGL (#14), a listening pass (#15) and a license (#16). Still for the owner: signing/notarizing and
publishing the macOS zip, and a version bump before any new release (`Tools/release.sh` uses the project's
`bundleVersion`, still 0.1.0, and won't overwrite the published 0.1.0 zip).

## Round 2 scope (2026-10-06, branch `improvements-2`)

Picked from the open list (#10, #11, #12) plus two things round 1 left short. Every item ends with the full check
suite passing: `sim.sh fuzz`, `unity.sh test`, `build-linux`, and `autopilot.sh` including the `ui` flow checks.
Screenshots go to `docs/media/improvements/round2/`, and automation output goes to the gitignored `Logs/` (not the
shared `/tmp`).

### S1. Finish R3: label size and window size

Round 1 measured label caps at about 16 px against an 18 px target. R3's scope also promised to clamp the windowed
size to the display, and that **was not done**. Round 1's report missed it.
- Labels: larger type and a slightly wider gutter. Long names may shrink a little, but caps stay at 18 px or more for
  names up to 10 letters at 1920×1080.
- Window: the windowed size is clamped to 90% of the display, keeping 16:9, both at startup and when leaving
  fullscreen.
- **Verify:** a crop of a short and a long label from a 1920×1080 Graveyard capture, with caps measured in pixels.
  An EditMode test for the size rule on 1366×768, 1280×800, 1920×1080 and 3840×2160. Tower and labels don't
  overlap the HUD at 1440×900.

### S2. Relaxed shifts (#11)

An assist toggle in Settings for players who keep getting fired.
- Guests' patience lasts 1.5× longer, and five complaints don't end the shift. Overtime ignores it, because it only
  ends at five complaints.
- Relaxed runs show a RELAXED stamp on the time card and don't save stars or best scores. Reaching the 1★ score in
  a relaxed run opens the next shift (a "relaxed clear", shown on the roster), the same way a star does.
- The intro card says when Relaxed is on. After a second firing on the same shift, the time card suggests it.
- **Verify:** EditMode tests (a relaxed sim is never fired, patience is 1.5×, relaxed clears unlock, stars aren't
  saved). A `sim.sh relaxed` table: modelled new players are never fired, and their relaxed 1★ rate is reported. An
  autopilot `ui` check that plays a relaxed shift in the built game. Screenshots of the intro, time card and roster.

### S3. Daily Overtime (#10)

A date-seeded Overtime with its own best, for a reason to come back.
- Once Overtime is open, its intro card gets a **TODAY'S SHIFT** button. Every run that day uses the same seed: the
  same building, deck and opening guests. Later spawns depend on how you play.
- The save keeps today's best and your best day. The time card shows "Today's shift · Oct 6" and today's best.
- **Verify:** EditMode tests (the seed is stable for a date and differs between dates; two sims with today's seed
  match tick for tick under the same bot; the daily record saves). An autopilot `ui` check plays the daily twice
  and compares the opening building and first guests. Screenshots.

### S4. Bigger guests at full zoom-out (#12), only if it holds up

Scale guest models up (about 12–15%) without changing the floor art, and check queues, the car with four riders,
the Mirror Mover's two spaces and the door frame.
- **Verify:** autopilot captures of Graveyard at rush hour and a full car, with guest height measured against round 1.
  If guests clip each other, the car or the doors in a way that reads badly, this is reverted and reported, not
  landed half-done.

Not this round: Windows (module), WebGL, the audio listening pass, signing, hosting and the license (owner), and
regenerating the trailer and README screenshots (heavy, and they're still honest about showing 0.1.0).

## Round 2 results (2026-10-06)

Three of the four items landed. S4 was tried and deliberately not landed. The final checks on the last build:
EditMode 38/38, `sim.sh fuzz` OK, and the full autopilot in a 1920×1080 window passed all ten shifts, 8 gamepad
checks and 18 flow checks, at 85 fps average. Screenshots are in `docs/media/improvements/round2/`.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| S1 label and window size | `0f97808` | Pixel cap height on text-only crops (same method for both rounds), `DisplayTests`, Graveyard captures at 1920×1080 and 1440×900 | Label caps 17 px → **20 px** (CRYPT, BOILER ROOM, LAUNDROMAT). Labels fade while their floor is mid-shuffle. The window clamp (owed from round 1) is covered by a unit test for six display sizes. No small display was available to see it. |
| S2 Relaxed shifts | `52c79a7` | 5 EditMode tests, `sim.sh relaxed 32`, autopilot `ui` (relaxed Wednesday, fired-twice suggestion) | Modelled new players are fired 0% relaxed (3–18% standard) and reach 1★ on 59–96% of shifts (40–53% standard). No stars or bests are saved, and a relaxed clear opens the next shift. Not tried by a person. |
| S3 Daily Overtime | `8e340ee` | 3 EditMode tests, autopilot `ui` with a pinned date (record, lower rerun, ONE MORE SHIFT replay) | Same building and first six guests on a replay. Today's best rolls over at local midnight. No online leaderboard. |
| S4 bigger guests | `24e0df1` (reverted change, captures kept) | Same-seed captures at 1.0 and 1.15 | **Not landed.** A full car at 1.15 pushes heads to the car ceiling and under the floor sign. It needs the car and floor art re-proportioned in Blender. |

Found along the way:
- **A test overwrote the real save.** The first run of the new SaveData tests called `Record`, which saves to
  `persistentDataPath` (shared by the editor and the player): `~/.config/unity3d/Nearby/One More Floor/save.json`.
  The file already existed (the directory dates from Oct 4) and its old contents are gone. Every SaveData test now
  sets `SaveData.Ephemeral`, `SaveData.Save` documents the trap, and later runs were checked to leave the file
  byte-identical. The overwritten file was left as the test wrote it. It might be recoverable from the machine's
  snapper snapshots (`/.snapshots`, root only), if those cover `/home`. That's for the owner.
- The S4 captures show ghost copies of riders in every floor's shaft opening when the car moves during kid stops,
  at both scales. It's probably the car showing through the doors as it passes, but it wasn't investigated.

Still open: bigger guests via an art pass (#12), Windows (#13, needs the module), WebGL (#14), a listening pass
(#15), a license (#16), signing, notarizing and publishing the macOS build, a version bump before the next release,
and regenerating the README screenshots and trailer, which still show the 0.1.0 look.

## Round 3 scope (2026-10-06, branch `improvements-3`)

Picked after a fresh baseline on `227c0b7`. `build-linux` passes, and the Graveyard autopilot passes. The `ui` flow checks
**fail one check on unchanged `main`**: "unplugging the controller in use pauses". Captures from those runs showed
two things a player meets every shift: reward popups that pile up into unreadable text at a group drop
(`+$345`, `AHH, SUNSHINE!`, `SUNKISSED!`, `DOUBLE DROP!` and `+$220` drawn over each other), and patience that's only
shown on small rings above small guests, while "stormed off waiting" is the biggest cause of complaints on every
shift (`sim.sh causes`). Every item ends with `sim.sh fuzz`, `unity.sh test`, `build-linux` and the autopilot
(all shifts, pad pass, `ui` flow checks) passing. Screenshots go to `docs/media/improvements/round3/`.

### T1. Rewards you can read

- Popups are laid out in screen space so they never draw over each other: a new popup moves up to clear the
  ones already showing. The group-drop banner is a single banner that upgrades DOUBLE → TRIPLE → FULL HOUSE
  instead of stacking a new one for each guest.
- **Acceptance:** while the autopilot plays every shift, the largest overlap between any two visible popups
  is ≤ 4 px in each direction (measured every frame on their text bounds). One drop banner per stop.
- **Verify:** an autopilot measurement printed per shift and checked, and before/after crops of a group drop.

### T2. Patience at a glance

- The waiting-count badges on the floor labels and the panel buttons show the patience of the floor's most
  impatient waiting guest: a ring around the count in the same green/amber/red as the guest bubbles, with
  the badge going amber under 50% and red (and pulsing) under 25%. Calm floors get a dark badge, not today's
  always-red one.
- **Acceptance:** label and panel badges agree with the guests' own rings. Nothing moves or resizes in the layout.
- **Verify:** an EditMode test for the core "lowest waiting patience on a floor" rule. An autopilot `ui` check
  that drains one waiting guest's patience and reads the label and panel badge colours. Screenshots.

### T3. Controller disconnect pause that holds up

- The baseline failure: auto-pause only fires if the last input was the gamepad, so a mouse nudge (or another
  window's pointer crossing this one) between the last button press and the disconnect stops the pause. Now a
  gamepad that was used during the running shift pauses it when it disconnects, whatever was touched last. A pad
  that was never used in the shift still doesn't pause it.
- **Acceptance:** the `ui` check passes deterministically. New checks: pad press, then virtual mouse movement,
  then unplug → paused with "Controller disconnected". An unused second pad unplugged → not paused.
- **Verify:** the autopilot `ui` checks, run twice.

### T4. Automation can't touch the real config

- Round 2's tests overwrote the real save. Since then the game has used an ephemeral save under automation, but
  Unity still writes its own `prefs` file there on every run. `Tools/play.sh` now points `XDG_CONFIG_HOME` at the
  gitignored `Logs/xdg/` whenever an automation flag is passed, so autopilot, demo and trailer runs keep
  everything in the repo.
- **Verify:** `sha256sum` of `~/.config/unity3d/Nearby/One More Floor/{save.json,prefs}` before and after every run
  this round, and the files appearing under `Logs/xdg/`.

### T5. README screenshots that match the game

- The ten README screenshots still show the 0.1.0 framing (no floor labels, smaller tower). Regenerate them from
  the current build with the game's own `TrailerReel` stills: a new `Tools/make_trailer.sh stills` mode runs only
  the video pass and writes only `docs/media/screenshots/`. The trailer, its poster and the teaser GIF are left
  alone (re-cutting the trailer is the owner's call).
- **Acceptance:** each screenshot still shows what its README caption says. Alt text and captions are corrected if not.
- **Verify:** look at every image. The README caveat about old media is narrowed to the trailer, poster and teaser.

Also to write up: the "ghost riders" from round 2's S4 captures, and why bigger guests are an art-and-framing decision,
not a scale tweak.

Not this round: bigger guests (#12, see the write-up), Windows (#13, needs the module), WebGL (#14), a listening pass
(#15), the license (#16), signing, hosting, releases, and the trailer (owner).

## Round 3 results (2026-10-06)

All five items landed. Final checks on the final code: EditMode **39/39**, `sim.sh fuzz` OK, and the full
autopilot on the last build in a 1920×1080 window passes all ten shifts (worst popup overlap 0.0 px on each),
8/8 gamepad checks and every `ui` flow check (now 21), at 76 fps average. The hashes of
`~/.config/unity3d/Nearby/One More Floor/{save.json,prefs}` matched before and after every build, test, autopilot and
capture run this round. Screenshots are in `docs/media/improvements/round3/`.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| T4 automation config isolation | `45b8c5f` | Hashes around every run. Unity's files appear under `Logs/xdg/` | Met for the player (autopilot, demo, trailer). The **editor** (build and test) still rewrites the game's `prefs` with identical bytes; its config dir also holds the shared Unity licence, so it was left alone. |
| T3 controller disconnect pause | `0df0976` | Autopilot `ui`: pad press, virtual mouse nudge, untouched spare pad removed (no pause), used pad unplugged (pause) | Met. **Correction to the plan:** the baseline failure wasn't the mouse. With no Input System settings asset, devices are disabled while the window is unfocused, so on this shared desktop the virtual pad's presses never arrived. The autopilot now sets `IgnoreFocus` for itself; players keep the default. The mouse-nudge gap was real too, and it's fixed. |
| T2 patience badges | `37fd402` | New EditMode test, autopilot `ui` (red at 15%, calm on a patient floor), `t2-*` captures | Met. The amber case is coded but wasn't hit by the seeded check (no third floor had guests waiting), so it's unverified in the built game. |
| T1 readable rewards | `7be27e0` | Per-frame overlap measurement on every shift, `t1-before-*` / `t1-after-*` | Met: 0.0 px everywhere. The first build measured 5–81 px, from a popup easing down into an older one and from the pop-in overshoot. Both were fixed before the commit. Flying coins and world signs aren't popups and can still cross the text (see the new `triple-drop.jpg`). |
| T5 README screenshots | `8984154` | Each image looked at against its caption | All ten regenerated with `make_trailer.sh stills`. One alt text corrected. The trailer, poster and teaser GIF are unchanged. |

Found along the way:
- **The S4 "ghost riders" are an automation artifact**, not a game bug. `FastForward` drains all 70 s of skipped
  deliveries in one frame, so every delivered guest's 2.2 s walk-out starts at once, one per floor. In normal play,
  guests leave only at the car's floor.
- **Why bigger guests are a design call:** a slot is 2.8 units (a 2.5-unit room, a 2.4-unit car interior), and nine
  slots have to fit the screen's height, so guests can't grow without the car. Thinner slabs and sign bands would
  win under 10%. A real gain means showing fewer floors at once (a closer default zoom) or a different floor
  layout. That's a judgment for the owner, not a scale tweak.
- A run that dies mid-coroutine leaves the player running until `autopilot.sh`'s 20-minute timeout. One did this
  round (the pad pass threw after its input was lost). It was stopped by PID.

Still open: bigger guests (#12, see above), Windows (#13, needs the module), WebGL (#14), a listening pass (#15),
a license (#16), signing, notarizing and publishing the macOS build, a version bump before the next release, and
re-cutting the trailer, poster and teaser GIF, which still show 0.1.0. Nobody has played these changes yet: the
badge colours and popup layout were checked by measurement and screenshots only.

## Round 4 scope (2026-10-06, branch `improvements-4`)

The ranked list is nearly used up: what's left (#12–#16) is blocked on the owner or on human ears. These items come
from a fresh look at what a player meets on a normal run, on a baseline at `b070a46`: `build-linux` passes,
EditMode 39/39, `sim.sh fuzz` OK, and the full autopilot (results below in Round 4 results). Every item ends with
`sim.sh fuzz`, `unity.sh test`, `build-linux` and the autopilot (all shifts, pad pass, `ui` flow checks) passing, and
the real `save.json` and `prefs` hashing the same before and after. Screenshots go to
`docs/media/improvements/round4/`.

### V1. Star progress during the shift

Stars decide whether the next shift opens, but nothing in play says how close you are: the star targets are on
the intro card and the time card only. The HUD card gets a star track under the tips: three stars that light
(with the star sound) as the score passes each target, and a line saying how much more the next star needs.
Overtime shows the best score as its target instead, and a relaxed shift shows the one target that opens the
next shift.
- **Acceptance:** at every frame of every autopilot shift, the lit stars on the HUD equal `Sim.StarCount`, and the
  caption names the next target. The card only grows downward, so it stays clear of the floor labels (which start at
  400 px). No overlap with labels or the tower at 1920×1080, 1440×900 and 2560×1080.
- **Verify:** a per-frame autopilot check on every shift, captures at the three aspect ratios, `uicheck.sh`.

### V2. Saves that survive a crash or a bad file

`SaveData.Save` truncates `save.json` and writes it in place. A crash or power cut mid-write leaves a broken file,
the next launch silently starts from nothing, and the first save then overwrites what was left. Now the save goes
to a temp file that replaces `save.json` in one step, keeping the previous version as `save.json.bak`. Loading
falls back to the backup, and a file that can't be read is kept aside as `save.unreadable-<time>.json` instead of
being overwritten. Loaded values are clamped (stars 0–3, no negative counts).
- **Acceptance:** EditMode tests, all in a throwaway folder under the project's `Temp/`, never
  `persistentDataPath`: a save round-trips, a truncated `save.json` loads from the backup, a garbage file with no
  backup starts fresh and keeps the garbage aside, and out-of-range values are clamped.
- **Verify:** the tests, and the real save's hashes around every run.

### V3. A guest guide you can open mid-shift

Eight guests and six shuffle cards are introduced one per day, and the only reminders are the hover card and
one-time tips. A **GUEST GUIDE** button on the pause card and the roster opens a page of every guest and card met
so far (from the shifts that are open), with its icon and rule, and locked silhouettes for the rest.
- **Acceptance:** it opens and closes from the pause card and the roster with mouse, keys and pad. It lists exactly
  the guests and cards from open shifts. The shift stays paused underneath.
- **Verify:** an autopilot `ui` check that opens it with the virtual pad from the pause screen, counts the entries
  against the save, and closes it. Screenshots with a fresh save and a full save. `uicheck.sh` hit test.

### V4. The amber patience badge, checked in the built game

Round 3 left the amber badge unverified: the seeded check only ran it when a third floor had guests waiting, and none
did. The check now reuses the red floor: it raises that guest to 40% and reads amber on the label and the panel.
- **Verify:** the `ui` flow check passes, not skipped.

Not this round: bigger guests (#12, waiting on the owner), Windows (#13), WebGL (#14), the listening pass (#15), the
license (#16), signing, hosting, releases and the trailer.

## Round 4 results (2026-10-06)

All four items landed. Baseline on `b070a46`: `build-linux` OK, EditMode 39/39, `sim.sh fuzz` OK, full autopilot
PASS (ten shifts, 8 pad, 21 `ui`, popups 0.0 px) at 103 fps (load ~12), with the amber badge check silently skipped.
Final checks on the final code: EditMode **47/47**, `sim.sh fuzz` OK, and the full autopilot on the last build in a
1920×1080 window passes all ten shifts, **12** pad checks, **25** `ui` checks, 10 popup checks and 20 new star-track
checks, at 88 fps average (load average 22–27 during the run; this machine is shared, so the fps isn't comparable with
the baseline's). Graveyard also passes at 1440×900 and 2560×1080. The hashes of
`~/.config/unity3d/Nearby/One More Floor/{save.json,prefs}` matched before and after every build, test and autopilot
run. Screenshots are in `docs/media/improvements/round4/`.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| V2 crash-safe saves | `7888a61` | 6 EditMode tests in a throwaway folder under `Temp/` | Met: round trip with backup, a truncated file loads the backup and is kept aside without pushing the backup out, garbage with no backup starts fresh and is kept, values clamped. A real crash or power cut mid-write wasn't simulated; the tests damage the file directly. |
| V1 star progress in play | `4e71ebd` | Per-frame autopilot check on all ten shifts, after-the-bell check, captures at 16:9, 16:10, 21:9 | Met. Monday (played to the bell) matched on 3538/3538 frames and lit all three; the other shifts play 60 s before a fast-forward, so most only exercised the unlit track during play and the final count after it. Making the card taller pushed the coach tip against it at 21:9, so the tip now keeps a 28 px gap. |
| V3 guest guide | `026f783` | 2 EditMode tests; autopilot pad path (Start, down, A; B back) with the clock frozen; roster path with a Monday–Wednesday save (3 guests, 5 cards); in-game click hit-test of the pause card, guide and roster | Met. The first full run failed the pad path: Start already lights the focus ring, so "down down A" landed on RESTART SHIFT. The test script was wrong, not the game. |
| V4 amber badge | `c077040` | Full autopilot `ui` | Met: the red floor's guest raised to 40% reads amber on the label and the panel. |

Also: `Tools/autopilot.sh` now resolves a relative output folder (`8270220`). Before, the player wrote `player.log`
under `Builds/Linux/` and the script reported "no autopilot output". Checked with `Tools/autopilot.sh Logs/r4/ap-relcheck ui`
(PASS, with the log in that folder).

Found along the way, not fixed:
- At 1920×1080 the coach tip box (right edge ~398 px) covers the first letter of a long floor label next to it
  ("BOILER ROOM" in `03_play_monday_18s`). This was already the case before this round. It needs the tip narrowed
  or the labels' gutter moved, and both change the layout that rounds 1–2 measured.
- `Tools/uicheck.sh` needs an editor session with a CLI bridge. The autopilot now does the same hit test in the
  built game for the pause card, guide and roster, but not for title, intro, settings or results.

Still open: bigger guests (#12, owner), Windows (#13, needs the module), WebGL (#14), a listening pass (#15), a license
(#16), signing, notarizing and publishing the macOS build, a version bump before the next release, and re-cutting the
trailer, poster and teaser GIF. The README screenshots predate the star track and the guide. Nobody has played these
changes yet.

## Round 5 scope (2026-10-06, branch `improvements-5`)

Picked from round 4's open items and a fresh look at a normal run on `af771cd`. Each item ends with
`sim.sh fuzz`, `unity.sh test`, `build-linux` and the autopilot (all shifts, pad pass, `ui` flow checks) passing, and
the real `save.json` and `prefs` hashing the same before and after. Screenshots go to
`docs/media/improvements/round5/`. Guest scale is untouched (still waiting on the owner).

### Y1. The forecast shown on the tower

The shuffle forecast is the game's planning tool, but it's text on the panel ("Office and Library", "Roll floors
4-6 up"), so every stop means reading names and finding them in the tower while guests wait. Now the floor
labels show what the next card will do: a small chip on each floor that will move, with an arrow and the slot it
lands in, and a JAM chip on a named floor the car is about to dock at (the anchor rule). The chips are exact once
the car is on its way (the card is played with the target as the anchor, after any floor leaves). Before that, they
follow the floor you're hovering or have picked with the pad, and otherwise assume you won't stop at a floor the card
moves. The SHUFFLE FORECAST setting hides them along with the panel cards.
- **Acceptance:** a core `ShiftSim.PreviewStop(anchor)` predicts the building after the next full stop. On every
  shift, with the bot playing, the building after every full stop equals the prediction made one tick before it.
  In the built game, the chips on the labels agree with that prediction on every frame of every autopilot shift.
- **Verify:** an EditMode test over all ten shifts (every full stop checked), unit tests for a jam, a block move
  around the anchor and a floor leaving before the card, a per-frame autopilot check, and captures.

### Y2. The coach tip never covers a floor label

At 1920×1080 the tip box's right edge (~398 px) covers the first letter of a long floor label next to it. The box
gets narrower, grows downward to fit its text, and is kept clear of the leftmost label every frame.
- **Acceptance:** on every autopilot shift, at every frame where a tip shows, the tip box and the floor-label pills
  don't overlap (0 px). Checked at 1920×1080, 1440×900 and 2560×1080. Every tip's text still fits inside its box.
- **Verify:** a per-frame autopilot measurement on rectangles, like the popup check, plus before/after crops.

### Y3. The autopilot plays every timed shift to the bell

Only Monday played to the end; the others stopped after a minute and fast-forwarded, so the star track was only
checked unlit during play, and rush hour never ran through the presentation outside Monday. Every timed shift now
plays to the bell (Overtime keeps its one-minute cap, since it only ends on complaints).
- **Acceptance:** the full autopilot passes, with "played to the bell" in each timed shift's line and stars lit
  during play on the shifts where the bot earns them. The script's timeout covers the longer run.
- **Verify:** the full autopilot output.

### Y4. README screenshots and status

Regenerate the README screenshots with `Tools/make_trailer.sh stills` so they show the star track and the forecast
chips, check each against its caption, and update the README and "Status and known issues". The trailer, poster and
teaser GIF are left alone (owner).

Not this round: bigger guests (#12, owner), Windows (#13), WebGL (#14), the listening pass (#15), the license (#16),
signing, hosting, releases and the trailer.

## Round 5 results (2026-10-07)

All four items landed. Final checks on the final code (`bc8e12b` and later; Y4 changed only docs and images):
EditMode **51/51**, `sim.sh fuzz` OK (now with 21,270 random-input stops checked against the forecast preview), and the
full autopilot on the last build in a 1920×1080 window passes **117 checks**: all ten shifts (every timed one to the
bell), 12 pad and 25 `ui` checks, at 79 fps average (load average 19 at the start, 42 by the end, since the machine is
shared). Graveyard also passes at 1440×900 and 2560×1080. The hashes of
`~/.config/unity3d/Nearby/One More Floor/{save.json,prefs}` matched before and after every build, test, autopilot and
stills run. Screenshots are in `docs/media/improvements/round5/`.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| Y1 forecast on the tower | `f3f6581` | 4 EditMode tests (jam, block move around the anchor, a floor leaving first, and every full stop of 3 bot runs on all ten shifts: 3,263 stops, 500 jams, 221 departures), the fuzz check, and two autopilot checks per shift | Met. On every shift of the full run, the chips matched the preview on every frame, and floors landed where the chips said at every stop (87–127 stops per timed shift). The chips follow hover and the pad's floor pick, so with a mouse resting on the tower they describe that floor's stop rather than the car's target. That's deliberate and matches the trip preview, but nobody has tried it. |
| Y2 tip clear of labels | `8004798` | Per-frame rectangle check on every shift, Monday at 1920×1080, Graveyard at 1440×900 and 2560×1080, before/after crops | Met: 0 px overlap with resting labels, and the text always fits (boxes 324–370 wide, against 360 before). Labels of floors mid-shuffle (faded, sliding) can still pass under the box, as they already did under the HUD card. |
| Y3 autopilot to the bell | `bc8e12b` | Full autopilot | Met: every timed shift rang the bell with rush hour, and the star track lit 3 stars during play wherever the bot earned them. The run takes about 11 minutes (was 7); the script's timeout is now 40. |
| Y4 README screenshots | `e61b1cf` | Each image looked at against its caption | All ten regenerated. They show the star track, and `graveyard-flip.jpg` shows a JAM tag. The other shots happen to land on a Calm card or a close-up, so they show no forecast chips. The trailer, poster and teaser GIF are unchanged. |

Found along the way:
- **The ocean moment's push-in carried the floor labels outward**, so for about 3 s the labels spread past the tip box
  and toward the HUD card. The first full Y3 run caught it on Saturday (the tip touched a label by 0.5 px). Labels now
  fade during a scripted push-in, as they already did in the close-up zoom (in `bc8e12b`).
- With an ephemeral save that carries over between shifts, one-time tips have all been seen by Wednesday, so the tip
  check had no tip frames to measure on Wednesday, Graveyard and Overtime in the full run. Graveyard's tips were
  measured in the single-shift runs at three window sizes.
- The README's tests section now describes the per-frame checks. `Tools/autopilot.sh`'s usage line says it takes
  `pad`/`ui` too.

Still open: bigger guests (#12, owner), Windows (#13, needs the module), WebGL (#14), a listening pass (#15), a license
(#16), signing, notarizing and publishing the macOS build, a version bump before the next release, and re-cutting the
trailer, poster and teaser GIF (still 0.1.0). Nobody has played rounds 4 and 5. In particular, whether the forecast
tags help or clutter the labels needs a person to judge.

## Round 6 scope (2026-10-07, branch `improvements-6`)

Picked from round 5's open items and a fresh look at the pause card on `72f16fb`. The ranked list's remaining
items (#12–#16) still wait on the owner or on human ears, and guest scale stays untouched. Each item ends with
`sim.sh fuzz`, `unity.sh test`, `build-linux` and the autopilot (all shifts, pad pass, `ui` flow checks) passing,
and the real `save.json` and `prefs` hashing the same before and after. Screenshots go to
`docs/media/improvements/round6/`.

### Z1. Restart and quit ask before they throw a run away

On the pause card, RESTART SHIFT sits two presses of "down" below RESUME, and QUIT TO ROSTER is one more. Either
one ends the run at once, unscored. Round 4's own pad test landed on RESTART by mistake, and a player can too. Now
the first press arms the button (it turns red and reads REALLY RESTART? / REALLY QUIT?, and the hint line says the
run won't count); a second press does it. Moving the focus or the mouse to another button, or closing the card,
disarms it.
- **Acceptance:** with the virtual pad, Start, down, down, A leaves the shift paused at the same sim time with the
  button armed; A again restarts (a fresh sim at time 0). A mouse click on QUIT TO ROSTER arms it, moving to
  RESUME disarms it, and two clicks quit. The pause card's click hit test still passes.
- **Verify:** autopilot `ui`/pad checks and a screenshot of the armed button.

### Z2. A controls card beside the pause card

The game's only controls reference is the README. The coach teaches a few inputs once, and right-click (let a rider
off here), Space (let everyone in) or the number keys are easy to miss. The pause card now has a CONTROLS panel
next to it, written for the input in use: mouse and keyboard, arrow keys, or the gamepad with the names of the
controller being held (Xbox, PlayStation or Nintendo, as the prompt strip does).
- **Acceptance:** the panel lists every input in the README's tables for that scheme, names the right buttons for
  each pad family, and doesn't overlap the pause card or leave the screen at 1920×1080, 1440×900 and 2560×1080.
- **Verify:** autopilot checks (text per scheme and family; rectangles inside the canvas and clear of the card)
  and screenshots for mouse and a PlayStation pad.

### Z3. The forecast tags say which stop they describe

Round 5's tags follow the floor you're hovering or have picked, else the car's target, and nothing on screen says
which. Now the label of the floor the tags assume you stop at carries a marker in the tag's place: a brass
**STOP** when it's where the car is going, and **STOP?** when it's a floor you're hovering or have picked (a
what-if). JAM still wins on a floor the card names. No marker when the tags assume no particular stop.
- **Acceptance:** on every frame of every autopilot shift, the marker is on exactly the previewed floor (STOP for
  the car's target, none without one) and the other tags still match the preview. A flow check hovers a floor
  while the car heads elsewhere and reads STOP? on it, with tags matching that floor's preview.
- **Verify:** the per-frame autopilot check, the flow check, and captures.

### Z4. One-time tips measured on every shift

With the autopilot's save carrying over between shifts, every one-time tip had been seen by Wednesday, so the
coach-tip check had nothing to measure on later shifts. The full run now clears the seen tips before each shift,
and the check fails a shift where no tip showed.
- **Acceptance:** the full autopilot reports tip frames > 0 and 0 px overlap on every shift.
- **Verify:** the full autopilot output.

### Z5. README screenshots and status

Regenerate the README stills if Z3 changes what they show, check each against its caption, add the pause card
with its controls panel, and update "Status and known issues" and this file.

Not this round: bigger guests (#12, owner), Windows (#13), WebGL (#14), the listening pass (#15), the license (#16),
signing, hosting, releases and the trailer.

## Round 6 results (2026-10-07)

All five items landed. Final checks on the final code (`0d679a2` and later; Z5 changed only docs and images):
EditMode **53/53**, `sim.sh fuzz` OK (21,270 random-input stops matched the forecast preview), and the full autopilot
on the last build in a 1920×1080 window passes **129 checks** (117 in round 5): all ten shifts to the bell, the pad
pass and every `ui` check, with no logged exceptions. Graveyard also passes at 1440×900 and 2560×1080. The hashes of
`~/.config/unity3d/Nearby/One More Floor/{save.json,prefs}` matched before and after every build, test, autopilot and
stills run. Screenshots are in `docs/media/improvements/round6/`.

| Item | Commit | Verified by | Result |
| --- | --- | --- | --- |
| Z1 restart and quit ask twice | `fdaa094` | Autopilot pad pass: 5 new checks | Met. With the pad, "down down A" arms RESTART SHIFT and the clock stays put; moving the ring disarms it; a second A restarts. With the mouse, one click arms QUIT TO ROSTER, pointing at RESUME disarms it, and two clicks quit. A 6 s timeout also disarms it. |
| Z2 controls panel | `46bcbd6` | 2 EditMode tests (eight rows per scheme and family; exact pad names per family), 4 autopilot checks (Xbox, PlayStation, Nintendo, mouse), a layout check at three window sizes | Met: the card and panel are on screen, apart, and every row's text fits its cell at 1920×1080, 1440×900 and 2560×1080. The arrow-key scheme is covered by the unit test only; no autopilot step drives the pause card with arrow keys. |
| Z3 STOP / STOP? markers | `48f45d1` | 3 new `ui` checks, the per-frame check extended to expect the markers | Met. In the full run the tags matched the preview on every frame of every shift, with a STOP mark showing on 356–1467 label-frames per shift, and floors landed where the tags said at every stop. STOP? only appears when a person points at a floor; the bot doesn't hover, so it's checked by the scripted `ui` step, not during the bot's shifts. |
| Z4 tips on every shift | `5a5ae24` | Full autopilot | Met: tip frames on every shift (89–894 in the final run), 0 px overlap, no spilled text. |
| Z5 README and stills | (this commit) | Each changed image looked at against its caption | `core-shuffle.jpg` now shows a STOP tag and `route-preview.jpg` a STOP? on the hovered floor. The other changed stills differ only by render noise (42–64 dB PSNR). A new `pause-controls.jpg` from the final full run shows the controls panel. The trailer, poster and teaser GIF are unchanged. |

Found along the way:
- **The new STOP check crashed the prompt strip** in the first full run (12 logged `ArgumentOutOfRangeException`s
  in `HudCursor.UpdatePrompts`). The pad pass leaves gamepad mode on, and the check then runs a shift with input
  off, so no floor had been picked (cursor slot -1). Only automation turns input off, so players couldn't hit it,
  but the strip now stays hidden until a floor is picked (`0d679a2`). The rerun passed with no exceptions.
- **Frame rate in the full runs is not comparable with round 5.** Single-shift runs on this branch averaged 65–103
  fps (round 5: 47–79), but the two full runs averaged 32 and 23 fps while seven other games and several editors
  shared the iGPU (load 16–35). No per-frame work was added outside the pause card, which only works while it's
  open, and the label tags already ran the same preview every frame in round 5.
- Unity's test runner rewrites `TestResults.xml` in the game's config folder (`~/.config/unity3d/Nearby/One More
  Floor/`) on every `unity.sh test`, as it did in round 5. It isn't the save or the settings, and redirecting the
  editor's config folder would also move the shared Unity licence, so it was left alone.

Still open: bigger guests (#12, owner), Windows (#13, needs the module), WebGL (#14), a listening pass (#15), a license
(#16), signing, notarizing and publishing the macOS build, a version bump before the next release, and re-cutting the
trailer, poster and teaser GIF (still 0.1.0). Nobody has played rounds 4–6. In particular, whether the forecast tags
help or clutter the labels, and whether STOP? reads as a what-if, need a person.

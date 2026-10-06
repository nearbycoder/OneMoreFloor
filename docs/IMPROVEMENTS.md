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

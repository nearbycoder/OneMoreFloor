# One More Floor: Design & Technical Plan

> **Pitch:** You run the elevator in The Shuffleton, a hotel that rearranges its floors every time you stop.
> Get the vampire, the houseplant and the soaked swimmer where they're going before they lose patience.

This plan is the contract for the build. Scope: **ten floors, eight passenger types, short score-based
shifts**, built at a high finish level. Anything listed under "stretch" is optional. Everything else is
the target.

---

## 1. Design pillars

1. **One verb that feels great.** Press a button and the car *goes*: there's a mechanical clack, a
   whoosh, the needle sweeps, a *ding*, the doors part, and passengers hop out with a coin burst. The
   car responds in under 100 ms, with no dead air and nothing to wait through.
2. **The building is a character.** Every stop sets off a *KA-CHUNK*: floors lurch out, slide past each
   other and settle with a bounce and a puff of dust. It's legible, telegraphed, and occasionally
   outrageous (the doors open onto the ocean).
3. **Rules you can explain in one line, combinations you can't plan for.** Every passenger type has a
   single rule that fits in an icon. The fun comes from the combinations: a vampire, a houseplant and a
   kid who pressed every button, all in one car.
4. **Short, replayable shifts.** Two or three minutes, a score, three stars, and a big "One More Shift"
   button. The goal is a player who finishes a run and immediately wants another.
5. **Cohesive whimsy.** The look is a mid-century Art Deco diorama with chunky peg people and warm
   lamplight. The soundtrack is lounge muzak that gets nervous when you do.

## 2. Core loop

**Moment to moment (each stop takes 2–5 s):**
1. The car **arrives** with a ding and a little spring settle, and the doors open.
2. **The building shuffles** around the docked floor, playing the next card from the forecast.
3. Passengers bound for this floor **hop out automatically**. You collect fare, tip and multipliers.
4. You **click waiting passengers** to let them in (or press Space to board everyone who fits).
5. You **pick the next floor**: click it in the building, press its panel button, or press a number key.
   The doors close and the car goes.

**Per shift (2–3 min):** spawns ramp up, building events (departures, ocean visits) arrive, the final
30 s is a rush hour, and then you clock out with a score, stars and a best score.

**Meta:** ten shifts across "a week at The Shuffleton". Each one introduces one new idea, unlocks the
next at 1★, and saves your best score and stars. Overtime (endless) unlocks at the end.

## 3. Mechanics in detail

### 3.1 The building
- A vertical **stack of slots** (5 in the first shift, up to 9 later). Each slot holds one **floor**
  (a themed room module). The elevator shaft runs up the left side of the stack.
- Slot 1 is at the bottom. Panel buttons and number keys **1–9** address slots. Clicking a floor in the
  building addresses whatever floor is in that slot when you click.
- **Floors can leave and arrive.** A floor in the building can be marked *departing* with a countdown
  in stops. When it reaches zero it detaches and drifts away into the clouds, and an off-site floor
  slides into the gap. The Ocean is a visiting floor that comes and goes this way.

### 3.2 Shuffles (the rearrangement rule)
- **When:** every time the car **stops at a destination you chose**, the next shuffle card plays while
  the doors open. The forced stops a kid causes (see below) don't count.
- **Anchor rule:** the floor the car is docked at **cannot move**. If a card involves the docked floor,
  it **jams**: grinding gears, sparks, a "JAMMED!" stamp, and the card is discarded. Docking at a floor
  is how a skilled player protects it.
- **Forecast:** the panel shows the **next two cards** as mini-diagrams with floor icons and arrows,
  like Tetris's next-piece queue. Casual players can ignore it. Skilled players plan with it.
- **Card types:**

| Card | Effect | Introduced |
| --- | --- | --- |
| Calm | Nothing moves (a breather) | Mon |
| Swap A⇄B | Two floors trade places | Mon |
| Rise X | X jumps to the top, the floors above it each drop one | Wed |
| Sink X | X drops to the bottom, the floors below it each rise one | Wed |
| Roll | A block of 3 adjacent floors rotates by one | Thu |
| Flip | A block of 4–5 floors reverses order (rare, big) | Graveyard |
| Depart X / Arrive Y | Scheduled by the event director (couriers, the ocean) | Fri |

- The deck for each shift is generated from per-shift weights, with guarantees: no more than 2 calms
  in a row, no repeating the same pair twice in a row, and the Lobby moves at most every third card.

### 3.3 The car
- **Capacity 4.** Passengers have a size of 1 or 2 (the Mirror Mover is 2).
- Travel feel: 0.18 s to accelerate, about 0.2 s per floor while cruising, 0.2 s to ease in, then a
  small spring bounce. Doors open in 0.28 s and close in 0.22 s.
- **Redirect mid-trip:** choosing another floor while moving makes the car ease toward the new target.
  The shuffle has already happened, so the layout is stable.
- **Dropping someone off early:** click a passenger in the car while docked and they step out to wait
  here with the same destination. They grumble, and their patience keeps draining. This is the escape
  hatch for capacity and conflict problems.
- The **bellhop** (your avatar) stands at the panel inside the car. They salute at stops, sweat when
  things are bad, and celebrate combos. They don't take up capacity.

### 3.4 Patience, complaints, fares
- Every passenger has **patience** (base 26 s, shorter in later shifts), shown as a ring around their
  bubble that turns green, then amber, then red, and pulses near zero.
- Patience drains **1×** while waiting and **0.5×** while riding, with per-type modifiers.
- **At zero while waiting:** they storm off to take the stairs. **Complaint.**
  **At zero while riding:** they get off, fuming, at the next stop. **Complaint**, no fare.
- **Fare** depends on type. **Tip** = fare × remaining patience fraction.
- **Service streak:** each delivery adds +0.1× (capped at ×3.0). Any complaint or loss resets it
  (with a record-scratch stinger).
- **Group drop:** each extra passenger delivered at the same stop gets +25% ("Double drop!",
  "Triple drop!").
- **Five complaints** means "You're fired!", and the shift ends early. You keep your score and stars
  are still awarded on it, so being fired costs you time rather than wiping the run.

### 3.5 Passenger types (8)

| # | Type | Size | Fare | One-line rule | Introduced |
| --- | --- | --- | --- | --- | --- |
| 1 | **Commuter** | 1 | 100 | Just wants their floor. | Mon |
| 2 | **Houseplant** | 1 | 150 | Won't get off until it's had **sun** (doors opened on a ☀ floor while it's aboard). Wilts 1.5× faster while riding until sunned. | Tue |
| 3 | **Mirror Mover** | 2 | 220 | Takes **two spaces**. Won't ride with a **vampire**. | Wed |
| 4 | **Vampire** | 1 | 180 | Won't ride with a **mirror**. If the doors open on a **☀ floor** while it's aboard, it bursts into bats and is lost. | Thu |
| 5 | **Courier** | 1 | 260 | Delivers to a floor that's **leaving the building**. Get there before the countdown hits zero. | Fri |
| 6 | **Swimmer** | 1 | 160 | Waits on the **Ocean**, which only visits for a few stops. Always "Lobby, please." | Sat |
| 7 | **Kid** | 1 | 120 | Pressed **every button**: the car stops at every floor on the way (quick stops, no boarding, no shuffle). | Sun |
| 8 | **Tycoon** | 1 | 380 | **Express only.** Any stop before theirs ruins the tip (fare only). Patience drains 1× while riding. | Mon II |

Sunny floors (☀): **Greenhouse** and **Ocean**.

Built-in combos this produces:
- **Plant + Vampire:** the plant needs the sun that destroys the vampire.
- **Kid + Vampire:** the kid's forced stops can open the doors on the Greenhouse.
- **Kid + Tycoon:** every forced stop ruins the express.
- **Mirror + Vampire:** they can't share the car, and the mirror's two spaces crowd everyone else out.
- **Courier + shuffles:** the destination is leaving, and the building keeps moving it.
- **Swimmer + Ocean:** a rescue window that closes, ending at a Lobby that might be anywhere.

Origin/destination tendencies (for flavour and readable patterns):

| Type | Spawns on | Heads to |
| --- | --- | --- |
| Commuter | any (Lobby-weighted) | any (Office/Lobby-weighted) |
| Houseplant | any non-sunny | any non-sunny |
| Mirror Mover | Lobby, Penthouse, Library | any |
| Vampire | Crypt-weighted | Crypt-weighted, never sunny |
| Courier | Lobby | a departing floor |
| Swimmer | Ocean | Lobby |
| Kid | Daycare-weighted | Daycare-weighted |
| Tycoon | Lobby / Penthouse / Office | Penthouse / Office / Lobby |

### 3.6 The ten floors

| Floor | Colour | Look | Notes |
| --- | --- | --- | --- |
| **Lobby** | Crimson & gold | Checkerboard marble, revolving door, reception desk + bell, palms, chandelier | Most traffic. Never departs. |
| **Office** | Mustard | Cubicles, water cooler, filing cabinets, desk lamps | Commuter and tycoon hub |
| **Library** | Walnut brown | Tall shelves of colourful books, rolling ladder, armchair, globe | |
| **Laundromat** | Aqua | Row of front-loaders (drums spin), folding table, baskets | |
| **Boiler Room** | Rust orange | Huge boiler, gauges, pipes, valve wheels, steam puffs, hazard stripes | |
| **Greenhouse** ☀ | Lime green | Glass roof with light shafts, potted plants, vines, watering can | Sunny |
| **Penthouse** | Royal purple | Grand piano, chandelier, champagne tower, velvet sofa, skyline windows | Mirror/Tycoon hub |
| **Crypt** | Slate + eerie green | Stone blocks, coffins, candelabras, cobwebs | Vampire home |
| **Ocean** ☀ | Ocean blue | Open sea level with the horizon, waves lapping the threshold, a buoy, gulls, a little pier at the door | Visiting floor |
| **Daycare** | Bubblegum pink | Ball pit, rocking horse, giant blocks, crayon drawings | Kid hub |

### 3.7 Shifts (levels)

All shifts are score-based, and **1★ unlocks the next**. Star thresholds are set by simulation (§13).
The first pass used instant-pointer bots (1★ = a sloppy bot, 2★ = a decent bot, 3★ = a strong one),
which made the second half of the week a wall for people. The final pass uses a **human model**
(`Bot.Human`): decision time, Fitts'-law pointer travel for every click, a beat to take in each stop,
and Space to board a whole queue. The model has three skill levels. 1★ is the median new player, so a
first-timer unlocks the next shift in a try or two. 2★ is the 60th percentile of average players, and
3★ is the 60th percentile of practised ones.

| # | Shift | Length | Slots | New thing | Purpose |
| --- | --- | --- | --- | --- | --- |
| 1 | **Monday · First Day** | 2:00 | 5 | Commuters, Swap/Calm | Teach board → send → shuffle. Gentle. |
| 2 | **Tuesday · Green Thumb** | 2:30 | 6 | Greenhouse ☀, Houseplant | Teach a detour (go get sun). |
| 3 | **Wednesday · Heavy Lifting** | 2:30 | 7 | Penthouse, Mirror Mover, Rise/Sink | Teach capacity (2-space passengers). |
| 4 | **Thursday · Fangs for Nothing** | 2:30 | 8 | Crypt, Vampire, Roll | Teach conflicts and sun danger. Dusk lighting. |
| 5 | **Friday · Special Delivery** | 2:30 | 8 | Courier, departing floors | Teach countdowns and the anchor trick. |
| 6 | **Saturday · Surf's Up** | 2:30 | 8 (+Ocean) | Ocean ☀, Swimmer | **Trailer moment** (scripted below). |
| 7 | **Sunday · Family Day** | 2:30 | 9 | Daycare, Kid | Chaos agent. |
| 8 | **Monday Again · Board Meeting** | 2:30 | 9 | Tycoon | Express routing under pressure. |
| 9 | **Friday the 13th · Graveyard Shift** | 3:00 | 9 (+Ocean) | Everything, Flips | Night finale with every combination. |
| 10 | **Overtime** | endless | 9 (+Ocean) | Continuous ramp, ends at 5 complaints | High-score chase. Unlocks after shift 9. |

**Scripted beats** (each shift opens with a short "show, don't tell" setup before the random director
takes over):
- *Monday:* one commuter in the Lobby wants the Office. A coach hand points at them ("Click to let them
  in"), then at the Office's button. On arrival the first shuffle is a big, slow, obvious swap, with
  the coach pointing at the forecast. The clock starts after the first delivery.
- *Tuesday:* a houseplant in the Lobby wants the Library. If you go straight there, it refuses to get
  off (a "☀?" bubble) and the Greenhouse button glows.
- *Wednesday:* a mirror mover and two commuters. The car can't fit them all, so the first lesson is
  about capacity.
- *Thursday:* a vampire boards, and a mirror mover waiting on the next floor refuses to get in
  (🪞✖🧛).
- *Friday:* the Library gets a "LEAVING IN 4" sign and a courier appears in the Lobby holding a box
  addressed to it.
- *Saturday (trailer moment):* you're carrying a commuter to the Library. During the trip the Ocean
  shoves the Library out of the building. The camera pushes in, the doors open onto open sea, gulls
  call, and a soaked swimmer squelches aboard: **"Lobby, please."** The commuter shrugs and picks a new
  floor.
- *Sunday:* a kid boards, and every button on the panel lights up at once.
- *Monday Again:* a tycoon in the Lobby taps a gold watch. The music adds a harpsichord.
- *Graveyard Shift:* lights out, candlelight, the full cast, and a Flip card in the first minute.

**Narrative frame (light, no walls of text):** you're the new operator at The Shuffleton. Each shift
opens with a one-line sticky note from "The Management" ("The building moves around a bit. Don't
take it personally."). Finishing the Graveyard Shift shows a final note, "Stay. —The Building", then a
short credits crawl, and Overtime unlocks.

### 3.8 Difficulty curve
- **Within a shift:** the spawn interval eases from about 4.2 s to 1.7 s along a smoothstep. The last
  30 s is **Rush Hour**: spawns ×1.4, fares ×1.5, and a music layer kicks in.
- **Across shifts:** slots go from 5 to 9, base patience from 30 s to 22 s, the waiting cap from 6 to
  14, shuffle intensity rises (more cards that aren't Calm, multi-floor cards, then Flips), and the
  type mix gets wider.
- **Overtime:** everything keeps ramping per minute until five complaints.
- **Tuned for people, not bots:** `Tools/sim.sh pace` searches each shift for the gentlest spawn pacing
  at which modelled average players are rarely fired (≤10%) and new players usually survive.
  `Tools/sim.sh causes` breaks complaints down by cause. That analysis is why later shifts now spawn
  more slowly than Monday (their rules carry the difficulty), why riding patience drains about 20%
  more slowly, why the Ocean stays a stop longer with one swimmer after Saturday, and why couriers get
  six stops of warning.
- **Real playtests close the loop:** every shift a person plays is logged locally
  (`playtests.jsonl`), and `Tools/playtest_report.py` turns those logs into suggested thresholds. It
  also checks the model's reaction-time assumptions against measured ones.

## 4. Controls & UX

| Input | Action |
| --- | --- |
| **Left click a waiting passenger** (docked floor) | Board them |
| **Left click a floor** in the building / a **panel button** | Send the car there |
| **1–9** | Send the car to slot 1–9 |
| **Space** | Board everyone who fits (in queue order, skipping conflicts) |
| **Right click a passenger in the car** (docked) | Let them off here to wait |
| **Left click a passenger in the car** | Send the car to their floor |
| **Esc / P** | Pause |
| **Hover** a passenger | Highlights their destination floor and button |
| **Hover** a floor/button | Highlights everyone who wants to go there |
| **Scroll wheel / Z / − =** | Zoom between the whole tower and a close-up that follows the car |

**Gamepad and arrow keys.** The d-pad (or Up/Down) moves a floor cursor, and left/right (or LB/RB,
Q/E) picks a guest on that floor or in the car. **A** (Enter) sends the car there, lets the picked
guest in, or lets everyone in when the car is already open there. **X** (F) lets a picked rider off,
**Y** (Space) lets everyone in, **B** unpicks, the triggers or right stick zoom, and **Start**
pauses. A prompt strip says what each button will do right now. The menus have a focus ring that the
d-pad, stick and arrows move; A/Enter confirms, B/Esc goes back, and left/right adjusts sliders and
switches.

- The panel is the "dashboard": each button's plate shows the **floor icon + name** of the floor in
  that slot (with a split-flap flip when floors move), **pips** for riders heading there, and a
  **count** of people waiting there.
- Refusals are visible: a passenger who can't board shakes their head and shows why (FULL, 🪞✖🧛).
- Zoomed in, every floor out of view with guests waiting gets an indicator at the screen edge (which
  floor, how many, the most impatient one's ring). Click one to send the car there.

## 5. Art direction

- **Style:** a stylized, chunky diorama. Mid-century Art Deco hotel meets surreal toy box (Wes
  Anderson's palette with Monument Valley's readability). Rounded bevels everywhere, no realistic
  textures. Colour comes from a **palette atlas**: one shared texture where each mesh face is UV-mapped
  to a swatch, with a matching mask for metallic and smoothness and an emission atlas for lamps,
  neon and screens.
- **Palette:** a deep indigo-to-peach dusk sky, oxblood brick and cream trim on the tower, brass
  fittings, and **one signature hue per floor** (see §3.6) repeated on its icon, plate and bubble
  rims so you can recognize a floor at a glance.
- **Characters:** 1.3–1.5 m "peg people" with capsule bodies, big heads, dot eyes and a single
  silhouette prop per type (briefcase, pot and leaves, giant gilded mirror, cape and collar, parcel,
  swim cap and towel, propeller beanie and balloons, top hat and monocle). Moving parts (leaves, cape,
  balloons, mirror) are separate objects so they can animate in code.
- **Animation (rigged):** every character has a Blender armature (root, hips, spine, head, two-bone
  arms and legs with skinned elbows and knees, plus capes, leaves, balloons, mirrors and propellers).
  Each one carries keyframed clips (Idle, Walk, Stomp, Tap, Cheer, Tuck, Fume, plus Salute and Worry
  for the bellhop) that the game blends with a Playables mixer from what the guest is doing. Code
  layers secondary motion on top: idle breathing (squash and stretch),
  impatient foot-tap jitter, a hop with squash on landing when boarding, a cheer spin when delivered,
  a storm-off stomp, plant leaves drooping as patience drops and perking up in the sun, the vampire's
  cape flare and hiss, balloons bobbing on springs, the swimmer dripping, and the tycoon puffing up.
- **Camera:** perspective with a narrow FOV (~22°) and a slight downward tilt, so it reads like an
  orthographic diorama with depth. The whole tower and the panel are always in frame. Gentle idle
  drift. Small shake on shuffles, bigger on jams and poofs. A cinematic push-in for scripted beats
  (the ocean).
- **Lighting:** a URP Forward+ key light with soft shadows. Each floor has a warm interior lamp tinted
  toward its hue. A gradient ambient. Each shift has a **time-of-day preset** (Mon morning, Tue noon,
  Wed afternoon, Thu dusk, Fri evening, Sat beach-bright, Sun golden hour, Mon II overcast morning,
  Graveyard night with moonlight and candles).
- **Post:** bloom (lamps, neon, lit buttons), tonemapping, gentle colour grading, vignette and SSAO.
  A chromatic-aberration pulse on Flips.
- **Environment:** the gradient sky, slow-parallax clouds, a distant skyline with lit windows, the
  street and pavement at the base, and on the roof a water tower plus a neon sign reading **THE
  SHUFFLETON** that flickers on at the title screen.

## 6. Audio direction

Everything is synthesized procedurally (numpy, run with Blender's bundled Python, rendered to WAV).
No samples or stock libraries.

- **Music: elevator muzak that responds to trouble.**
  - *Going Up* (gameplay, 104 BPM bossa in F): stems all exactly 16 bars long, started
    sample-synchronously:
    1. *bed*: FM Rhodes comping plus upright bass and brushes/shaker (always on)
    2. *melody*: vibraphone (when calm)
    3. *trouble*: pizzicato and tremolo strings, plus a ticking woodblock
    4. *rush*: congas, timbales and a horn stab layer (rush hour, big streaks)
    5. *night*: organ and harpsichord colour (Graveyard, Thursday, Tycoon aboard)
  - **Trouble meter** (0–1): rises with the lowest patience among passengers, complaints, a vampire
    aboard near the sun, and departures about to happen. Mapped to: melody ducking, the trouble layer
    fading in, a tape-warble pitch wobble (all stems share pitch, so they stay in sync), and a gentle
    low-pass at the extreme. As things calm down, it all relaxes back into lounge.
  - *Lobby Lounge* (title, 90 BPM, laid-back), plus stingers: shift start (ding-dong and a brass hit),
    rush hour, clock-out, star chimes ×3, new record, fired (sad trombone), and the final note.
- **SFX (~45):** button press (bakelite clack plus relay), hover tick, doors open/close (slide plus
  thunk), arrival ding (two-tone, up versus down, pitched per slot), motor hum loop (pitch follows
  speed), cable whoosh, shuffle (gear ratchet, stone grind, clunk and bounce), jam (grind and sparks),
  floor depart/arrive whooshes, splash/waves/gulls (ocean ambience), board hop, exit cheer, coin and
  tip cha-ching, streak tick-up, group drop fanfare, complaint (grumble and rubber stamp), storm off,
  vampire bats poof, plant sun sparkle and "ahh", mirror shimmer, courier stamp, kid giggle and button
  mash, tycoon harrumph and coin shower, refusal buzz, timer ticks, clock-out punch, UI hover, click
  and back, star chimes, and record.
- **Voices:** "animalese" gibberish with formant-filtered syllable blips in a different register per
  type (squeaky kid, deep tycoon, breathy vampire, gurgly swimmer, leafy rustle-voice plant).
- **Mix:** music around −18 LUFS-ish, SFX on top, music ducks under big stingers. Volume sliders for
  master, music and SFX.

## 7. UI / UX

- **Title:** the camera glides up the tower at dusk, the neon sign flickers on, and a plate menu offers
  *Start Shift*, *Duty Roster*, *Settings* and *Quit*. Muzak plays.
- **Duty Roster (level select):** a punch-card rack with one card per shift showing the day, title,
  new-mechanic icon, best score and stars. Locked cards are greyed with a padlock.
- **Shift intro card:** a sticky note from The Management (one line), plus a "New today" panel with the
  passenger rendered in 3D, its rule icon and one sentence. Click to start, and it counts down "Doors
  opening…".
- **HUD:** at top left, a rolling-odometer **tips** score and a **streak** multiplier badge. At top
  centre, a **clock** (time remaining) that goes red-tick for the last 10 s. At top right, the
  **complaint box** (5 slots stamped with red faces). On the right, the **operator panel**. In world
  space, **speech bubbles** with patience rings and rule badges. The forecast sits on the panel.
- **Feedback popups:** floating "+$212" in coins, "Double drop!", "Express!", "Sunkissed!", "Rescued!",
  "Just in time!", "JAMMED!", "Poof!".
- **Pause:** Resume, Restart, Settings, Quit to Roster. **Settings:** master/music/SFX volume,
  fullscreen, screen shake on/off, reduced motion, and show the forecast (on by default).
- **Results ("Clock Out"):** a time card slides in and gets stamped with tips, deliveries, best streak
  and complaints. Stars thunk in one at a time with rising chimes, then NEW BEST if earned. Buttons:
  **One More Shift** (default, Enter), Next Shift, Roster.
- **Save:** JSON in `persistentDataPath` holding per-shift best score and stars, unlocks, settings, and
  whether the tutorial has been seen.
- **Onboarding:** no text walls. A coach hand points at things, the first instance of each rule is
  scripted with a highlight, and a "New today" card has one sentence plus an icon. Hovering
  highlights destinations. Refusal feedback teaches the conflicts.

## 8. Game feel & juice list

- The car starts moving within a frame of the click. The pressed button visibly depresses (5 mm) and
  glows.
- The car squashes and stretches subtly on accel and decel, with a spring bounce on arrival. Cables
  vibrate. The counterweight moves the opposite way. A pulley wheel at the top spins.
- The indicator needle sweeps with overshoot. The arrival ding is pitched per floor.
- Doors use eased slides with a tiny overshoot and a seam light that spills out when they open.
- Shuffles: floors lift toward the camera, cross, and settle with a bounce, a dust puff, a camera
  nudge and a clunk. A jam throws sparks and grinds with a small shake.
- Departures: anchors pop, the floor drifts up and sideways into the clouds while the stack holds,
  and the arriving floor swings in from the other side.
- Passengers hop in with a parabolic arc and squash, and cheer-spin out. Coins burst from the
  delivery point and fly to the score counter, and the score rolls.
- Streak multiplier: the badge pulses and changes colour at ×2 and ×3, and the music adds a layer.
- Patience rings pulse red, and the passenger taps their foot faster and shows anger puffs.
- Vampire poof: a cloud of bats bursts out of the car, a small shake, and a puff of smoke.
- Plant sunning: sparkles and a quick little growth spurt.
- Kid: every button lights in a cascade, and the car "blips" at each floor.
- Ocean: animated waves with foam at the threshold, a splash when the swimmer boards, and drips in
  the car.
- Rush hour: a lighting pulse and a banner slam.
- Clock-out: time slows down briefly.
- UI: hover scale with spring, click punch, slide-in panels, and number count-ups.

## 9. Code architecture

```
Assets/
  Scripts/
    Core/         Pure C# simulation, no UnityEngine (testable, deterministic, seeded)
      Ids.cs            FloorId, PassengerKind, enums
      Defs.cs           FloorDef, KindDef, ShiftDef (data)
      ShiftCatalog.cs   all 10 shifts + scripted beats
      Building.cs       slots, floors, departing countdowns, shuffle application
      Shuffle.cs        card types, deck generation, jam rule
      Passenger.cs      state machine (Waiting, Boarding, Riding, Delivered, Left)
      Car.cs            capacity, motion profile, quick-stops
      ShiftSim.cs       the game: Tick(dt), commands, rules, scoring, events
      Director.cs       spawns, events (departures, ocean visits), rush hour
      Bot.cs            greedy/lookahead AI player (balancing + autopilot)
      Rng.cs            deterministic xorshift
    Game/         Unity presentation
      GameRoot.cs       bootstrap, scene flow, screens
      ShiftRunner.cs    owns a ShiftSim, routes input to commands, events to views
      BuildingView.cs / FloorView.cs / CarView.cs / PassengerView.cs
      PanelView.cs      3D button panel + plates + forecast + dial
      CameraRig.cs      framing, shake, push-ins
      Sky.cs            time-of-day presets, clouds, skyline
      Fx.cs             particle presets (dust, coins, sparks, bats, sparkles, drips)
      Tween.cs          tiny tween/easing helpers
      Library.cs        loads models/materials/icons from Resources
    Audio/        AudioDirector (music stems + trouble), Sfx (pooled one-shots), Voices
    UI/           Hud, Bubbles, Title, Roster, Intro, Pause, Settings, Results, Coach, Toasts, UiKit
    Save/         SaveData (JSON)
    Automation/   AutoPilot (bot drives the real game, captures screenshots, PASS/FAIL)
  Editor/         BuildScript, ProjectSetup (URP/renderer/materials/fonts), import postprocessors
  Tests/EditMode/ rules tests, determinism, bot-beats-every-shift, fuzz
ArtSource/        Blender generator scripts (+ saved .blend), previews
Tools/            unity.sh, build.sh, play.sh, autopilot.sh, synth/ (audio generator), contact sheets
```

- **Sim/presentation split:** `ShiftSim` exposes commands (`Board(id)`, `SendTo(slot)`,
  `DropHere(id)`, `BoardAll()`) and raises events (`CarArrived`, `ShuffleApplied`, `Jammed`,
  `PassengerSpawned`, `Boarded`, `Delivered`, `Complaint`, `Poofed`, `FloorDeparted`, `FloorArrived`,
  `RushHour`, `ShiftEnded`, …). Views react to the events and animate. The sim owns *all* timing that
  matters to gameplay (car motion and door timings), so the bot and the tests see exactly what the
  player sees.
- **Determinism:** each shift run is seeded. The same commands at the same ticks produce the same
  result (tested).
- **Content as code:** `ShiftCatalog` defines every shift's slots, weights, ramps, deck weights, beats
  and stars.
- **No scene wiring:** `Main.unity` holds only a bootstrap. `GameRoot` builds everything from
  `Resources` (models, materials, audio, fonts), following the pattern that worked in the reference
  projects.

## 10. Asset list (Blender, all scripted with `bpy`)

`ArtSource/omf_lib.py` provides the shared helpers: palette-atlas UV assignment, bevelled boxes,
capsules, cylinders, tori, lathe profiles, and FBX export in Unity axes.

- **Tower:** shaft frame (steel and brass, cables, pulley, counterweight), roof cap (water tower,
  antenna, neon sign frame), base and pavement, and the floor-module shell (floor slab, back wall,
  side walls, ceiling, and the elevator door frame with sliding doors).
- **Car:** brass cage with a cutaway front, interior lamp, handrail, the bellhop's panel nook, and
  two door leaves.
- **Operator panel (separate):** brass plate with Deco engraving, 9 buttons (separate objects),
  plates, the indicator dial with its needle, the forecast window frame, and a Board-All button.
- **Floors (10):** each a shell plus 4–8 signature props (see §3.6).
- **Characters (9):** commuter, houseplant, mirror mover, vampire, courier, swimmer, kid, tycoon and
  the bellhop. Animated parts are separate objects.
- **Environment:** skyline blocks (with emissive windows), cloud puffs, street lamp, gulls, a bat
  (for the poof effect), coin, and a dust puff mesh.
- **Icons:** 10 floor icons and 10 rule/kind icons rendered from Blender (orthographic, flat lit,
  outlined) to PNGs on transparent backgrounds.
- **Review:** each model is rendered with Eevee from the game angle, assembled into contact sheets,
  and inspected before import.

## 11. Milestones

| M | Deliverable | Exit criteria |
| --- | --- | --- |
| M0 | Plan, project scaffold, git, tooling scripts | Project opens and builds empty, committed |
| M1 | **Core sim + greybox prototype** | Click to board, send, shuffle, deliver, all in greybox. Feels snappy. Screenshots reviewed. Sim tests pass. |
| M2 | Art pipeline + tower/car/panel/floors/characters | Contact sheets reviewed, models imported, greybox replaced |
| M3 | Full content: 8 types, 10 floors, 10 shifts, beats, coach | Every shift playable start to finish, and the bot beats every shift |
| M4 | Audio: SFX, voices, music stems, trouble director | All events voiced, music reacts, levels sane |
| M5 | UI: title, roster, intro, HUD, pause, settings, results, save | Full flow with no dead ends, and progress persists |
| M6 | Polish: particles, lighting presets, post, camera, transitions | Screenshots at final quality |
| M7 | Validation: autopilot in the real build, fixes | Autopilot PASS, clean logs |
| M8 | README, Linux build in `Builds/`, final verification | Build runs, docs honest |

I commit at the end of every milestone, and more often in between.

## 12. Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Editor won't start (CachyOS libxml2) | `Tools/.libs/libxml2.so.2` shim via `LD_LIBRARY_PATH` in `Tools/unity.sh` |
| Player hangs on XWayland | Launch with `-force-wayland` (`Tools/play.sh`) |
| Readability with 9 floors on screen | Greybox screenshot at 9 slots early, bubbles as screen-space UI with a minimum size, signature colours, hover highlights |
| Shuffles feel random and unfair | Forecast queue, anchor/jam rule, never move a floor mid-trip |
| Too many rules at once | One new rule per shift, scripted first instance, a rule badge on every bubble |
| I can't *listen* to the audio | Proven synthesis recipes, numeric checks (peak, RMS, clipping, loop seam continuity), spectrogram images. Stated honestly in the README. |
| TMP essentials in batch mode | Import the TMP package resources from an editor script and generate SDF font assets ahead of time |
| Shared machine load | Batch mode, `blender -b --threads 6`, close editors when done, kill only my own processes |
| Scope creep | Stretch goals are marked. Content is code-defined, so tuning is cheap. |

## 13. Validation plan

- **EditMode tests (Unity Test Framework, batch mode):**
  - rules: capacity, Mirror⇄Vampire refusal, vampire poofs on a sunny door, plant holds until
    sunned, the kid forces quick stops without shuffles, the tycoon express penalty, the courier
    deadline, the swimmer lost when the Ocean leaves, the jam rule, patience and complaints, firing at
    five;
  - determinism: same seed and commands give the same final state hash;
  - **every shift is beatable:** the bot plays each shift with 5 seeds and must reach ≥1★ on all of
    them, and a strong bot must reach 3★ on at least one seed (keeping 3★ achievable but hard);
  - fuzz: 300 random-command runs per shift with no exceptions, invariants held (capacity, a unique
    floor per slot, passengers conserved).
- **Autopilot in the real built game:** `-omfAutopilot` makes the bot play shifts through the
  presentation layer, captures screenshots at key moments, and prints PASS/FAIL. It fails on any
  exception in the log.
- **Gamepad in the built game:** the autopilot drives the menus and a shift through a virtual
  Input System gamepad (`PadSim`). It checks the focus ring, back, the floor cursor, sending, zoom, and
  pause/resume.
- **Audio audit:** `Tools/synth/audit.py` measures every file (BS.1770 loudness, true peak, DC, clicks,
  loop seams, leading silence, spectral balance, stereo) and every `Sfx` call as the game plays it
  against its category. With `--mix` it also measures a recording of the final in-game mix.
- **Manual review:** play-mode screenshots of every shift, menu and results screen, reviewed and
  iterated.

## 14. The 5-minute prototype test

A new player sits down, and within five minutes:
1. **0:00–0:20:** they click a commuter, press a button, and *feel* the car go: the clack, whoosh,
   needle and ding. They understand the loop without reading anything.
2. **0:20–0:40:** the building shuffles for the first time. *KA-CHUNK*. They laugh or say "wait, what?"
   They notice the forecast.
3. **1:00:** they get their first **Double drop** and see the streak climb to ×1.5. The music swings.
4. **1:30:** they make a mistake (a complaint stamp, a record scratch), the muzak warbles, and they
   recover.
5. **2:00:** they clock out with 2★ and see exactly how many tips 3★ needs. The **One More Shift**
   button is right there.
6. **2:30–5:00:** Tuesday's houseplant refuses to get off ("☀?"), they work out the Greenhouse detour
   and grin.

**Pass criteria:** they press *One More Shift* or *Next Shift* without being prompted. To get there,
**Monday must be fun with commuters alone**, so M1 has to make the bare loop (board, send, shuffle,
deliver, score) feel good in greybox before any content gets built on top of it.

## 15. Stretch goals (only after M8)
A per-passenger musical motif layer, a photo mode, a daily-seed Overtime, and a
WebGL build.

# Neon Laser Corridor — First-Person Parkour

A first-person parkour run through a neon laser corridor (Unity 6.3, URP, new Input System).
Sprint, slide, wall-run, vault and double-jump through ~250 m of moving laser hazards.
Get hit or spotted and you fail, then respawn at the last checkpoint.
A **UV flashlight** reveals hidden tripwires, route markers and collectible shards.

---

## Setup (one time)

1. **Window → TextMeshPro → Import TMP Essential Resources** (needed for the HUD).
2. **File → New Scene → Basic (URP)**, then save it as `Assets/Scenes/ParkourCourse.unity`.
3. **GameObject → Create Empty**, name it `CourseBuilder`, set its position to `0,0,0`, then **Add Component → Parkour Course Builder**.
4. Press **BUILD COURSE**, then **Ctrl+S**.
5. Press **Play** and click the Game view to lock the mouse.

The builder creates:

- the corridor, player, camera rig, game manager, HUD, sun, fog and a post-processing volume (Bloom, Vignette, ACES tonemapping, Color Adjustments)
- a `Player` layer, if it doesn't exist yet
- the HUD font: **Inter SemiBold** (SIL Open Font License), copied from the `com.unity.dt.app-ui` package into `Assets/Parkour/Fonts/`
- the materials and the post profile, saved to `Assets/Parkour/Generated/` so you can tweak them

Everything it builds is a normal scene object. You can edit it by hand or with ProBuilder after building. Pressing **BUILD COURSE** again wipes and regenerates the course.

---

## Controls

| Key | Action |
|---|---|
| **WASD** | Move (momentum based) |
| **Mouse** | Look |
| **Shift** | Sprint (holding W for 0.6 s also sprints) |
| **Space** | Jump · double jump (in the air) · wall-jump (on an orange wall) · climb a chest-high ledge |
| **Ctrl / C** | Crouch. While sprinting it's a **slide**. |
| **F** | UV flashlight on/off (uses battery, recharges when off) |
| **R** | Respawn at the last checkpoint (on the finish screen: run again) |
| **Backspace** | Restart the whole run |
| **G** | Debug view: every detection volume and ray, plus labels |
| **Esc** | Free the cursor (click the Game view to lock it again) |

### Moves (`ParkourPlayer.cs`)

- **Jump:** coyote time 0.12 s (you can still jump just after leaving a ledge), jump buffer 0.15 s (a press just before landing still counts), and variable height (tap for a short hop).
- **Air control:** you can steer in the air, but steering never adds speed.
- **Double jump:** once per airtime. It refreshes on the ground, on walls and on ledges.
- **Slide:** crouch above 5.5 m/s. You get a speed boost, low friction, and you speed up down ramps. It fits under the 1.1 m slide bars.
- **Wall-run:** only on **orange** walls (`WallRunSurface`). Hold W and you get up to 1.5 s of low gravity. Space = wall-jump.
- **Vault / mantle:** ledges up to 1.1 m are vaulted automatically. Ledges up to 1.75 m are climbed with Space. The crosshair turns cyan when a ledge is in reach.

### Camera feel (`ParkourCamera.cs`)

- **Head bob:** follows the distance you've travelled, so footsteps match your speed.
- **FOV kick:** 78° up to 88° at top speed, plus 6° more while sliding.
- **Tilt:** 12° away from the wall during a wall-run, plus a small lean when strafing.
- **Landing dip:** a spring that scales with how hard you land.

**No jitter:** the player moves once per frame in `Update` (CharacterController, no Rigidbody). The camera is a child of the player, mouse look runs before movement (`DefaultExecutionOrder(-100)`), and all camera effects run in `LateUpdate`.

---

## Hazards → Physics checks (graded part)

Every hazard derives from `ParkourHazard` and does **one** kind of query against the `Player` layer.
In normal play the fail screen just says **ZAPPED / SPOTTED / FELL**, like a normal game. With the **debug view on (G)**, a detection feed appears bottom-left (e.g. **`CheckCapsule - Rotating Laser Arm`**) and the fail screen also names the check.

| Section | Hazard (script) | How it moves | Physics check |
|---|---|---|---|
| S1 | **Low Laser** (`LaserBeam`) | bobs a little just above the floor (jump) | **`Physics.CheckCapsule`** along the beam |
| S1 | **Sweeping Laser Gate** (`SweepingLaserGate`) | slides left↔right | **`Physics.CheckBox`** the size of the gate |
| S2 | **Rotating Laser Arm** (`RotatingLaserArm`) | 2 arms spin flat over the island (jump them) | **`Physics.CheckCapsule`** per arm |
| S3 | **Pulsing Scanner Wall** (`PulsingScannerWall`) | drifts back and forth, pulses ON/OFF, blinks before ON | **`Physics.CheckBox`** (only while ON) |
| S3 | **Laser Windmill** (`RotatingLaserArm`, tipped 90°) | arms spin across the corridor | **`Physics.CheckCapsule`** per arm |
| S3 | **Bobbing Orbs** ×6 (`BobbingOrb`) | bob up and down over a gap, out of phase | **`Physics.CheckSphere`** |
| S4 | **UV Tripwires** ×5 (`LaserBeam` + `UVReveal`) | static, vertical and sliding; invisible without UV | **`Physics.CheckCapsule`** along the wire |
| S5 | **Sentry Eyes** ×2 (`RailSentryEye`) | ride a rail and sweep their gaze | **`Physics.Raycast`**: (1) the visible laser pointer ray, and (2) line-of-sight rays to your head and chest inside the vision cone, which lock on after 0.35 s. Cover blocks both. |
| S5 | **Swinging Orb** (`SwingingHazard`) | pendulum across the corridor | **`Physics.CheckSphere`** around the ball |
| all | **Laser Pit** (`ParkourGameManager`) | — | height check (`y < -2.5`) |

Other Physics queries in the project:

- `Checkpoint` / `FinishLine` → `Physics.CheckBox`
- `UVCollectible` pickup → `Physics.CheckSphere`
- the player's headroom test and mantle clearance → `Physics.CheckCapsule`
- wall-run / ledge probes → `Physics.Raycast`

## UV flashlight (graded part)

`UVFlashlight.Illuminates(point)` is true only when **all four** of these hold:

1. the light is **on**
2. the point is within the light's **range** (16 m)
3. the angle between the light's forward and the point is **≤ spotAngle / 2** (the cone)
4. a **`Physics.Raycast`** from the light to the point hits **no geometry** in between (occlusion)

`UVReveal` asks this every frame for a few sample points along each hidden object (so a long tripwire lights up wherever the cone touches it). It enables the object's renderers only while lit, plus a 0.15 s linger so it doesn't flicker.

Hidden objects: the 5 tripwires, the violet floor arrows (route markers before each wire and pointing to shards), and the 6 **UV shards**. A shard can only be picked up while it's revealed.

The course itself is lit normally (sun, gradient ambient, neon strips, point lights, bloom). The UV light is for *finding* things, not for seeing the level.

## Debug view (G)

Draws every detection volume and ray as lines **in the Game view**. It uses runtime GL lines, so it shows in recordings without the Gizmos button.

- Hazard volumes are **green** when clear and **red** while touching you.
- Sentry cones are magenta, with their sight rays drawn (green = blocked by cover, red = sees you).
- The UV cone is a ring with its occlusion rays (yellow = lit, grey = blocked by a wall).
- Checkpoints and the finish are cyan boxes, and shards show their pickup spheres.
- Each hazard gets a floating label with its check name, e.g. `CheckSphere / Swinging Orb`.

The same shapes also draw as Gizmos in the Scene view.

---

## Course map (player runs +Z)

| z | Section | What you do |
|---|---|---|
| 0–46 | **S1 Warm-up** | Ramp, jump the low laser, time the sweeping gate, two gap jumps → **CP1** |
| 46–100 | **S2 Wall-run hall** | Wall-run right → island with rotating arm → wall-run left → double-jump gap → **CP2** |
| 100–152 | **S3 Slide & vault** | Slide bar, vault box, pulsing scanner, slide bar, mantle a 1.6 m ledge, windmill, orb gap → **CP3** |
| 152–190 | **S4 UV tunnel** | Roofed and darker. Turn on UV: 5 hidden tripwires (jump / slide / time / dodge / jump), markers, 3 shards → **CP4** |
| 190–252 | **S5 Sentries** | Cover pillars vs. the ceiling sentry, crouch/slide behind the low wall past the wall sentry, swinging orb, kicker ramp jump → **FINISH** |

The timer starts on your first move. Your best time is saved (PlayerPrefs). The finish screen shows time, best time, shards and fails.

---

## Demo video shot list

1. **Intro (5 s):** start pad, wide look down the corridor. Point out the bloom, neon edges and the clean HUD.
2. **Movement (20 s):** sprint (FOV widens), jump the gaps, show a coyote jump off an edge, slide under a bar, vault the box, mantle the ledge (crosshair turns cyan).
3. **Wall-run (10 s):** wall-run both orange walls (camera tilts), wall-jump off one, then the double-jump gap.
4. **Press G (debug on)** and keep it on for the hazard shots:
   - **CheckBox:** walk into the sweeping gate. The box turns red, the fail screen says "CheckBox - Sweeping Laser Gate", and you respawn at the checkpoint. Then pass the scanner wall while it's dark.
   - **CheckCapsule:** touch the rotating arm on purpose and read the feed (debug view on). Then show the windmill capsules spinning.
   - **CheckSphere:** time the bobbing orbs, then get hit by the swinging orb once.
   - **Raycast:** S5 sentry. Show the green rays when you're behind cover, then step out so they turn red, the eye locks on and you fail. Then crouch behind the low wall and show the rays blocked.
5. **UV light (20 s):** in S4 with F off, walk into an invisible tripwire and fail ("CheckCapsule - UV Tripwire Low"). Respawn, press F, and the wires and markers appear. Show the cone (debug ring) and that a wire **disappears behind a wall corner** (occlusion ray turns grey). Collect a shard, which can only be picked up while lit.
6. **Finish (10 s):** debug off, a clean run of the last section, kicker jump, finish screen with time, best, shards and fails.

## Tuning

Every number is a public field in the Inspector:

| Object | What to tune |
|---|---|
| `ParkourPlayer` (on Player) | speeds, jump, slide, wall-run, mantle heights |
| `ParkourCamera` (on Player) | sensitivity, FOV kick, bob, tilt, landing dip |
| Each hazard | speed, period, size, lock-on time |
| `ParkourGameManager` | kill height, fade times, respawn grace |
| `UVFlashlight` (on Main Camera) | battery |

## Scripts (`Assets/Scripts/Parkour/`)

| File | Role |
|---|---|
| `ParkourPlayer.cs` | movement motor (all parkour moves) |
| `ParkourCamera.cs` | mouse look + camera feel |
| `ParkourGameManager.cs` | run flow, checkpoints, fail/respawn, timer, detection feed |
| `ParkourHUD.cs` | UGUI + TextMeshPro HUD, fail/finish screens, fades |
| `ParkourDebugDraw.cs` | G debug view (GL lines) + `DebugShapes` helpers |
| `ParkourHazard.cs` | hazard base class (check name, fail report, debug label) |
| `Hazards/*.cs` | `SweepingLaserGate`, `PulsingScannerWall`, `RotatingLaserArm`, `BobbingOrb`, `SwingingHazard`, `RailSentryEye`, `LaserBeam` |
| `UV/*.cs` | `UVFlashlight`, `UVReveal`, `UVCollectible` |
| `Checkpoint.cs`, `FinishLine.cs`, `WallRunSurface.cs` | course pieces |
| `ParkourLayers.cs`, `ParkourPalette.cs` | layer masks and the color palette |
| `ParkourCourseBuilder.cs` | editor builder (BUILD COURSE / CLEAR) |

The old stealth version is still in `Assets/Scripts/Stealth/`, untouched.

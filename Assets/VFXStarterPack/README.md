# Unity VFX Starter Pack — Three-Cylinder Showcase

This Unity 6 pack turns the included OBJ showcase meshes and PNG particle textures into three animated, reusable Particle System prefabs. The build tool is editor-only; the effects use Unity's built-in Particle System and need no runtime scripts or VFX Graph.

## Build the pack

1. Let Unity finish importing `Assets/VFXStarterPack`.
2. In Unity, choose **Tools → VFX Starter Pack → Build Animated VFX Pack**.
3. Open `Assets/VFXStarterPack/Showcase/VFX_Showcase.unity` and press **Play**.

The builder creates three current prefabs under `Prefabs/`, reusable particle materials under `Materials/`, portrait material slots, and a scene with all three prefabs. If you already built the older pack, its four separate elemental prefabs are left in the project; the rebuilt showcase scene uses only the combined elemental prefab.

## The three prefabs

### `VFX_Transformation_Celestial`

Uses the Celestial OBJ mesh around its cylinder, with a rotating ground sigil, pink/gold star particles, violet/pink trail ribbons, and an automatic one-shot ascension burst.

### `VFX_Elemental_Bending`

Uses the Elemental Bending OBJ mesh and combines all four elements around one cylinder:

- **Fire:** rising orange/red particles and embers.
- **Water:** flowing blue/cyan droplets and trails.
- **Earth:** green/tan debris with gravity and rotation.
- **Air:** pale wind particles with stronger noise and trails.

Each element has its own child group under the prefab root. Particle colors and emission settings can be tuned independently.

### `VFX_FinalBoss_Apocalypse`

Uses the Final Boss OBJ mesh with a dark-purple aura, crimson embers, violet lightning flashes, rotating energy rings, trails, and an automatic overload burst.

Continuous Particle Systems are set to loop and play on awake. Burst systems are one-shot and play when the prefab activates.

## Add your own character PNGs

Each cylinder has a front-facing `Character_Portrait_Display` panel. The display is a flat panel on the front of the cylinder, not a texture wrapped around the curved surface.

1. Import a PNG into `Assets/VFXStarterPack/Textures/Portraits` (or another folder under `Assets`).
2. Select the matching material:
   - `Materials/VFX_Portrait_Transformation.mat`
   - `Materials/VFX_Portrait_Elemental.mat`
   - `Materials/VFX_Portrait_FinalBoss.mat`
3. In the Material Inspector, drag your PNG into **Base Map** (URP) or **Main Texture** (Built-in pipeline).
4. If the PNG has transparency, enable **Alpha Is Transparency** in its Texture Import Settings.

Each prefab has a separate portrait material, so you can set different images for all three. To change only one scene instance, duplicate its portrait material and assign the copy to that instance's `Character_Portrait_Display` Mesh Renderer. The PNG is referenced as a texture; it is not copied into or baked into the OBJ.

## Customize the effects

- **Color and intensity:** Select the relevant Particle System child. Adjust **Start Color** and **Color over Lifetime**. To brighten particles, use a brighter color and, when supported by the selected shader, increase its material emission.
- **Speed and flow:** Change **Start Speed**, **Start Lifetime**, **Velocity over Lifetime**, and **Gravity Modifier** in the Main and velocity modules.
- **Emission:** Adjust **Rate over Time** or the burst count in the Emission module. Keep looping rates moderate for performance.
- **Shape and motion:** Use Shape, Noise, Rotation over Lifetime, Size over Lifetime, and Trails modules on the child systems.
- **Scale:** Move or scale the prefab root to position the effect. Its Particle Systems use Local simulation space.

Particle materials use the included alpha textures and are configured for transparent blending (not opaque square billboards). They prefer the URP unlit particle shader, with built-in unlit fallbacks. If a shader is unavailable in your render pipeline, assign a compatible transparent/unlit particle shader to the material.

## Reuse on another GameObject

Drag a prefab from `Prefabs/` into a scene and position its root around your target. To use only its effect on a character or prop, make a prefab variant, then remove or disable `Starter_Showcase_Mesh` and `Character_Portrait_Display`; keep the particle-system children. The original showcase prefab remains unchanged.

## Included starter assets

- `Meshes/`: the original Transformation, Elemental Bending, and Final Boss OBJ meshes and MTL files.
- `Textures/`: the original `SoftGlow`, `MagicSpark`, `EnergyRing`, `FlameParticle`, `WaterDroplet`, `EarthShard`, `WindStreak`, and `LightningBolt` PNG textures.
- `Textures/Enhanced/`: an added set of transparent, tintable particle masks for soft bloom, starburst, rune ring, flame, water drop, earth shard, wind ribbon, and branching lightning. The builder assigns these to the effect materials by default; the original starter PNGs remain unchanged.

The OBJ meshes provide static decorative geometry. The child Particle Systems provide the animation.

## Unity Particle Pack additions

The prefabs also include nested effects from Unity Technologies' free **Particle Pack | Starter Assets** under `Assets/UnityTechnologies/ParticlePack/EffectExamples`. The source package files are left untouched. The builder nests FireFlies and EnergyExplosion into the transformation prefab; FlameStream, BigSplash, EarthShatter, and SandSwirlsEffect into the four elemental groups; and PlasmaExplosionEffect and ElectricalSparks into the final-boss prefab. These are ready-made Particle System prefabs from the imported pack.

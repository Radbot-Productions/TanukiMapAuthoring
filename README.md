> **All four editor setup guides now live in [Tanuki Map Editors](https://github.com/Radbot-Productions/TanukiMapEditors).** Start with [Blender installation and Workshop setup](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/blender.md), or choose [Unity](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/unity.md), [Godot](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/godot.md), or [Unreal](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/unreal.md). This is the older Unity-only repository.

# Unity setup

Tested on Windows x64 with **Unity 6000.6.7f1**, installed through [Unity Hub](https://unity.com/download). The ready project uses Built-in rendering; URP integration is optional.

1. Download **Tanuki-Unity-0.2.0-preview.3.zip** from the [editor release](https://github.com/Radbot-Productions/TanukiMapEditors/releases/tag/editors-v0.2.0-preview.3).
2. Extract to a new writable folder. In Unity Hub, add the folder containing Assets, Packages and ProjectSettings, and open it with the tested editor.
3. Wait for imports/compilation, then open **Tanuki → Map Editor**.
4. Choose **New → Create Empty Map**, Room, or Courtyard. Save the scene and assets normally.

For an existing project, keep the kit in a permanent folder. In **Window → Package Manager → Install package from disk**, select `Packages/com.radbot.tanuki-map-authoring/package.json` inside the kit. Do not select the project's manifest.json. Save/commit before replacing an existing plugin.

Keep scenery beneath the map root. Use Unity colliders for blocking geometry; colliders may be separate from visible meshes. Keep one boundary and at least four starts. The Components menu adds starts, boundaries, lights, Lighting Settings and Skybox. Lighting Settings selects Cartoon Street Day/Night. Enable Override Sun to edit rotation, color, lux and shadows through the normal Inspector. Additional lights remain independent. The generated character references show scale and are excluded from export.

Create materials through **Tanuki → Materials** and edit their Tanuki shaders with Unity's normal Material Inspector. The texture/scalar controls match portable inputs; arbitrary Standard/URP shader graphs are not equivalent. Do not directly edit generated environment helpers.

## Play in Tanuki

Press **Play in Tanuki**. It automatically finds the active scene's single map and exports its current state, including unsaved scene edits. **No manual map save/export, executable picker, or Workshop refresh is required.** Close and relaunch after further edits; this is a fresh snapshot, not live synchronization.

The first use downloads the separate [Windows preview player](https://github.com/Radbot-Productions/TanukiMapEditors/releases/tag/player-v0.2.0-preview.3), about **4.34 GB**. Progress appears in the editor. The plugins verify the download and install a shared copy under `%LOCALAPPDATA%/RadbotProductions/TanukiMapPreview`; missing/outdated players install automatically, and a failed startup can trigger one repair. Keep internet access available for first installation or repair. Windows prerequisites may need installation on a fresh computer. No game source, Unreal installation, or .NET installation is needed for this self-contained preview helper.

The player uses Tanuki climbing/gliding/movement and Tengu movement. **Escape** opens the menu to switch character/free camera or exit. It contains the authored materials, collision, lights and selected base lighting. Native editor rendering can differ in exposure and shading.

## Steam Workshop

Open Steam and sign in with Tanuki Tag access. Opening the Tanuki panel attempts a Steam connection automatically. Open browses subscribed maps; Export creates or updates an item. **Refresh** updates the displayed list; it is not required for local map preview. Save the editor project after publishing to retain the Workshop item association. Opening another creator's map forks its identity.

## Troubleshooting and compatibility

- New exports use **schema 5**, including editable sun overrides. This preview player reads schemas 1–5. The in-game Workshop runtime also needs schema-5 support before it can play newly exported maps; updating an editor does not update the Steam game automatically.
- Exactly one map root must exist in the active scene. An ambiguous scene produces an error instead of choosing a random map.
- A running preview retains its old snapshot. Close it and launch again after edits, especially lighting changes.
- If the setup helper is missing, reinstall the complete editor kit, rather than GitHub's Source code ZIP. Check editor status and preview logs for download/startup failures.
- Save projects before replacing plugins. Keep user maps and materials outside plugin installation folders. Report the editor/plugin version and relevant log in an [issue](https://github.com/Radbot-Productions/TanukiMapEditors/issues).

<<<<<<< HEAD
See [material inputs](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/PortableMapMaterials.md) and [light units](https://github.com/Radbot-Productions/TanukiMapEditors/blob/main/docs/PortableMapLights.md).
=======
New exports include optional names, hierarchy, local transforms and collider
settings. Older files reconstruct from their baked runtime data. Original Unity
prefab links, scripts and arbitrary editor components cannot be recovered from a
runtime map. Across Unreal/Unity, primitive colliders and starts may be flattened
to preserve their runtime placement under nonuniformly scaled hierarchies.

The samples are deliberately small blockouts made from ordinary primitives and
flat colors. Room has a floor, three walls, three blocks and a ramp. Courtyard
has a floor, scattered blocks, a cylinder, a low platform and a ramp. Each has
one boundary and four starts. There are no decorative buildings, foliage or textures.
New templates live under `Assets/TanukiBlockoutSamplesV2`; existing scenes are preserved.

### Create a new map

1. Open **Tanuki > Map Editor > New**.
2. Choose **Create Empty Map**, or **Start From Sample** and select Courtyard or
   Room. Starting from a sample creates a new scene and identity.
3. Author geometry under the map root. Keep its world scale at one; its local
   frame is the export origin. Child mesh transforms are baked.
4. Add Unity colliders to blocking surfaces. Visible geometry does not imply collision.
5. Use **Tanuki > Components > Player Start** to place at least four starts. Position
   its center above the floor and rotate its cyan forward arrow.
6. Keep the same **Map ID** for revisions. New map roots get unique IDs.
7. Every new root includes one Map Boundary. Select it to choose Box or Cylinder;
   use Unity's Transform tools to position, rotate and resize it. A cylinder needs
   equal X/Z scale. Its transparent preview is not exported as scenery.
8. Choose **Map Editor > Export**, then **Steam Workshop** or **Map File**.
   Both validate the current scene automatically. Export requires static geometry,
   exactly one active boundary and at least four active Player Starts.

The map root Inspector owns the map name, stable ID, optional preview image and
export shortcuts. The dialogs report errors and support corrected retries.
Components support Undo and are also available
under **GameObject > Tanuki** and **Add Component > Tanuki**.

## Export directly to Steam Workshop

**Tanuki > Export Map > Steam Workshop** publishes the current
scene without a separate manual file export. Windows is supported by this prototype.

1. Run Steam, signed into a developer account authorized for Tanuki.
2. The panel displays the connected Steam account. Use **Connect** to reconnect
   after starting Steam or signing in. Publishing does not require the game executable.
3. The title defaults to the map name. Leave **Preview PNG** empty to generate a framed map overview,
   or choose your own PNG smaller than 1 MiB. The generated view uses the Scene
   view's orientation and fits all map geometry; other scene objects/scripts are excluded.
4. Choose **Publish** for a new map or **Update Workshop** for your existing map.
   The panel exports a fresh `.tanukimap` and publishes directly through the running
   Steam client, showing pending state and the final result. Credentials stay in Steam.
5. The item ID is assigned automatically and remembered on the map root and in
   reload-recovery state. Save the Unity scene to retain its publishing association.
   A failed/uncertain submission can still have created an item; retain its ID and
   inspect the job log before creating another item.

The package includes editor-only Steamworks.NET bindings and the matching Windows
Steam API redistributable. It initializes the Tanuki app context and uses the logged-in
account. No game process, password prompt or developer credential is used. These
dependencies are excluded from player builds. See `Editor/SteamworksNET/PROVENANCE.md`.

Uploads survive Unity script reloads, retaining their SDK call and item IDs. Closing
Unity or entering Play mode during an upload reports an uncertain outcome and keeps
the recorded item ID for recovery. Files/receipts remain under `Library/TanukiWorkshop`;
only `content/map.tanukimap` is sent as Workshop content. The preview and diagnostics
remain outside the content directory. Do not delete pending job files.

Use a dedicated map-authoring project if another Steam integration is already loaded:
the plugin refuses to take over that integration's Steam context. Supported publishing
is Windows Editor in Edit mode; local file export remains usable without Steam.

Tanuki's Workshop remains **Only Developers** during testing. Item visibility is
shared within that restricted Workshop. The uploader does not change the hub's
access settings or accept Steam legal agreements. If Steam requests agreement
acceptance, complete it yourself in Steam, then update the recorded item ID.

## Supported content

The courtyard is a small outdoor sample with four exterior shops, shoji-style
windows, terracotta roofs, faceted trees, a gate, planters, benches, crates,
lanterns and four player starts. Its meshes and materials are generated locally;
no asset-store downloads are required. Shops are static exterior structures.
The Unity preview camera and light are outside the export root; their lighting
does not transfer to Tanuki. Give the sample a new Map ID when using it as the
starting point for a different map.

Share/load the saved `.tanukimap` file. It contains the JSON manifest and GLB,
including geometry, PNG textures, collision and player starts. There are no
required sidecar files or folders, Unity assets, AssetBundles or C# assemblies.
The batch diagnostics API can explicitly write loose JSON/GLB inspection copies;
normal authoring exports do not.

- Static readable triangle meshes, including nested, scaled and mirrored mesh transforms.
- Standard/URP Lit base-color textures, scalar metallic/smoothness, opaque/cutout materials.
- Box, sphere, capsule and static non-convex MeshCollider collision.
- PlayerStart position, facing and optional spawn tag.
- Up to 10,000 entries per category and a 256 MiB GLB payload.

Unsupported shaders/material features, extra layered materials, trigger/convex
colliders, sheared primitive colliders, LOD groups, Terrain, skinned meshes and
rigidbodies produce errors. Select/bake one LOD and convert Terrain to meshes.
Normal/metallic textures, emission, translucency and texture tiling/offset are
not supported yet. Mesh Read/Write must be enabled; texture Read/Write is not required.

Unity scripts, animation, lighting and game logic do not transfer. PlayerStart
markers select native or configured cooked classes in the game; they do not
generate Unreal Blueprint assets.

## Format

The `.tanukimap` container has a 16-byte little-endian header: four-byte `TMAP`
magic, uint32 version `1`, UTF-8 manifest length, and GLB length. Those two
payloads follow. The manifest records a SHA-1 geometry digest for accidental
mismatch detection, not authentication.

Manifest schema `1` contains `mapId`, `sceneSha1`, `meshes`, `colliders` and
`markers`. GLB vertices are baked relative to the root. Manifest transforms are
Unreal centimetres: `(X,Y,Z) = 100 * Unity (Z,X,Y)`; quaternion components are
`(Unity Z, Unity X, Unity Y, Unity W)`.

## Distribution roadmap

Git installation is the prototype path. OpenUPM submission is planned for
release. Steam Workshop will distribute exported maps separately from this
package. Unreal and Godot authoring frontends will target the same map contract.

## License

MIT. See [LICENSE.md](LICENSE.md). This repository contains only the Unity
kit; it does not include Tanuki game source, game assets or the Unreal runtime.
Steamworks.NET source retains its MIT notice; the Steam API DLL is Valve's
redistributable and is not relicensed as Tanuki code.

### Preview movement

Choose **Tanuki > Materials > PBR / Cutout / Transparent** to create an editable
sample copy in the selected Project folder. Shader definitions and sample materials
live in the plugin under `Shaders` and `Runtime/Materials`. Use Unity's normal
Material Inspector: the shader is **Tanuki/PBR**, **Tanuki/Cutout**, or
**Tanuki/Transparent**, with only portable inputs. PBR includes optional emission.
Roughness is exposed directly; metallic/roughness maps use green for roughness and
blue for metallic, multiplied by their scalar factors. Disable sRGB for those maps
and AO, and import normal images as Normal Map. Colors/scalars work without maps.
No separate material window or preview rebuild is required.

The tested editor is Unity **6000.6.7f1**. Built-in rendering uses the core package.
URP projects also install `Integrations~/URP/package.json` as a package from disk;
its **Tanuki Materials for URP** shaders remain inside that plugin, rather than
being generated in project Assets. Legacy Standard/URP Lit materials still export;
**Tanuki > Materials > Use Tanuki Shaders on Selected Map** converts their supported
inputs to the dedicated shaders.

New/Open adds a separate **Player Character** object. Select Third Person or
Free Camera in its Inspector, or press F during Play. WASD moves; Space jumps (hold to glide);
Shift runs or releases climbing. Free Camera uses Q/E vertically and Shift to
fly faster. Hold right mouse to capture the cursor; Escape releases it.

The upright proxy uses the basic Tanuki capsule (42 cm wide, 60 cm tall).
Its normal Tanuki speed profile is 2.7 m/s, or 4.5 m/s with Shift, with
acceleration and reduced air control. A 7 m/s jump impulse and 9.8 m/s² gravity
give about 2.5 m of jump height. Hold Space airborne for 0.2 seconds to glide
when at least 1 m above ground; release to resume falling. Glide approaches
1.5 m/s descent with up to 10 m/s steering. The profile follows authored OWL
defaults, GAS attributes and the normal Tanuki role multiplier; it does not
simulate role changes or temporary gameplay effects.
Static-map climbing supports attachment, idle retention, tangent movement,
corner following and top-out. It approximates object-form movement, without
wall jumps or sculpted-body collision. Use Unreal for final traversal checks.
The helper and all descendants are excluded from exports and thumbnails.
Unity's Input Manager must be enabled (Legacy or Both).

For actual game behavior, use **Play in Tanuki** in the map editor window.
**Choose Tanuki Preview** selects `TanukiMapPreview.exe` from the shared Windows
gameplay package. It exports a temporary snapshot and launches the production
Tanuki pawn, movement, climbing, gliding and camera. The Unity-native helper
described above remains an approximate local blockout tool.

Escape opens Resume, Free Camera / Tanuki Movement, Restart at Spawn, Settings
and Exit Preview. Free camera uses WASD, Q/E, mouse look and Shift for speed.
Closing the player removes its temporary map snapshot. No Unreal editor install
is required. The same gameplay package is used by Blender, Godot and Unreal.

>>>>>>> origin/main

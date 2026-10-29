# Tanuki Map Authoring

A Unity authoring kit for portable Tanuki maps. Create scenes in Unity, place
markers, validate the supported content, and export one `.tanukimap` file for
the Unreal runtime prototype. No Unreal Editor is needed for authoring.

**Prototype / prerelease.** This package exports static scenery, collision and
player starts. Workshop publishing and map scripting are not implemented, and
this is not yet an OpenUPM release.

## Install with Unity Package Manager

Open **Window > Package Manager**, choose **Add package from Git URL**, and paste:

```text
https://github.com/Radbot-Productions/TanukiMapAuthoring.git#v0.1.0-preview.1
```

Git must be installed and available to Unity. The declared minimum is Unity
2021.3. Validation currently uses Unity 2023.1.0b18; other editor versions have
not yet been qualified. Built-in and URP/Lit materials are supported within the
subset below. For local development, choose **Add package from disk** and select
`package.json`.

## Author a map

1. Open **Tanuki > Map Authoring**.
2. Choose **Open Sample Map**, or **Create Map Root**. The sample generator creates
   a scene, checker material, texture and PlayerStart prefab in
   `Assets/TanukiMapSample`. It does not replace an existing sample.
3. Author geometry under the map root. Keep its world scale at one; its local
   frame is the export origin. Child mesh transforms are baked.
4. Add Unity colliders to blocking surfaces. Visible geometry does not imply
   collision. **Add Box Collider** works on a selected map object without one.
5. Choose **Add Player Start**, or place the sample PlayerStart prefab. Position
   its center above the floor and rotate its cyan forward arrow.
6. Keep the same **Map ID** for revisions. New map roots get unique IDs.
7. Choose **Validate Map**. This performs the same complete conversion as export,
   including texture readback, without writing export files.
8. Pick a folder and choose **Export .tanukimap**. Export validates the current
   scene again. **Show Exported File** reveals the result.

The panel reports errors and supports corrected retries. Root/player-start
creation and collider additions support Undo. Components are also available
under **GameObject > Tanuki** and **Add Component > Tanuki**.

## Supported content

Share/load `map.tanukimap`. The `map.json` and `scene.glb` files alongside it are
inspection outputs. Geometry and PNG textures are embedded; the runtime needs
no Unity project, assets, AssetBundles or C# assemblies.

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

# Changelog

## 0.1.0-preview.5

- Start Workshop helper jobs in the neutral entry world for both source and packaged game executables, keeping rendered splash/lobby UI out of the graphics-free uploader.

## 0.1.0-preview.4

- Added Export & Upload to Workshop and Export & Update Workshop Item to the authoring window.
- Uploads export the current scene and use Tanuki's Steam backend through an immutable file job.
- Remember Workshop item IDs per map, track pending/results across editor reloads, and retain job logs for interrupted submissions.
- Requires Windows and a Tanuki build with the editor-job uploader; current testing uses the developer-only Workshop.
- Leaving the preview image empty generates a framed map thumbnail in an isolated preview scene.

## 0.1.0-preview.3

- Export now uses a file-save dialog and writes only the chosen `.tanukimap` file.
- JSON, GLB, textures, collision and player starts are embedded in that single file; normal exports no longer write loose inspection copies.
- Failed replacement preserves the previous valid file and supports corrected retries.

## 0.1.0-preview.2

- Added a stylized courtyard sample with timber shops, terracotta roofs, shoji windows, faceted trees, an entry gate and courtyard props.
- Made Open Sample Map open the courtyard. The original room remains available as the basic export regression fixture.
- Added Unity preview lighting/camera outside the export root and a rendered sample preview helper.

## 0.1.0-preview.1

- Added the Tanuki Maps editor panel for choosing/creating map roots, placing player starts, adding box collision, validation and export.
- Added Undo support for map/player-start creation and collider authoring.
- Added the sample room and PlayerStart prefab generator.
- Added static GLB geometry, embedded base-color textures, primitive/triangle collision and player-start export into one `.tanukimap` file.
- Made validation and export share the same conversion path.

This is an authoring prototype. Portable gameplay
scripting, embedded Tanuki gameplay preview, and Unreal/Godot authoring frontends
are planned separately. This package has not been submitted to OpenUPM.

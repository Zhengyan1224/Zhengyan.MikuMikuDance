# Zhengyan.MikuMikuDance Architecture

This repository is a clean C#/.NET 10 implementation inspired by nanoem's feature set.
The original `nanoem` tree is kept as reference material only; new code lives under the
`Zhengyan.MikuMikuDance.*` namespace and project prefix.

## Projects

- `Zhengyan.MikuMikuDance.Core`: domain model, animation timeline, project state, editing commands.
- `Zhengyan.MikuMikuDance.Formats`: PMD/PMX model readers and writers, VMD/NMD motion readers and writers, `.zmm` project JSON reader/writer, `.nma` project archive reader/writer, PMMv1/PMMv2 legacy project reader, PMMv2 legacy project writer, ASCII `.x` accessory reader/writer, and MME `.fx` structure reader.
- `Zhengyan.MikuMikuDance.Rendering`: renderer-neutral contracts and MME effect runtime metadata.
- `Zhengyan.MikuMikuDance.Rendering.OpenGL`: Silk.NET OpenGL/GLFW host.
- `Zhengyan.MikuMikuDance.App`: command-line entry and preview window.
- `Zhengyan.MikuMikuDance.Tests`: unit tests for parsing, animation, and project state.

## Compatibility Scope

The intended compatibility target follows nanoem's documented areas:

- PMD/PMX model data: vertices, materials, bones, IK, morphs, labels, rigid bodies, joints, soft bodies.
- VMD/NMD motion data: bone, morph, camera, light, model, accessory, and self-shadow keyframes.
- Project editing: timeline, selection, transform, copy/paste, undo/redo, draw order, transform order.
- Runtime: OpenGL rendering, skinning, edge rendering, toon textures, shadows, physics, audio/video integration.
- UI: viewport, timeline, model/accessory/morph/camera/light/play panels, preferences, model editor, effect editor.

## Current Milestone

The first milestone establishes a compileable cross-platform .NET 10 solution:

- Core MMD data model.
- PMD binary model reader/writer for current core model sections.
- PMX binary model reader/writer for current core model sections.
- VMD binary motion reader/writer for current core motion tracks.
- NMD protobuf motion reader/writer for current core motion tracks, annotations and effect parameters.
- DirectX `.x` ASCII accessory mesh reader/writer.
- MME `.fx` structure reader for parameters, annotations, techniques, passes and pass states, plus renderer-neutral effect metadata for semantics, script commands, normalized pass state, offscreen target metadata/default-effect conditions, offscreen drawable draw-plan decisions, multi-pass execution planning, basic GLSL shader source translation and shader uniform metadata.
- Versioned `.zmm` JSON project state reader/writer for scene, timeline, camera, light, model, accessory, mesh and motion references.
- `.nma` project archive reader/writer using a zipped `manifest.zmm` and embedded model, motion and accessory entries.
- PMMv1/PMMv2 legacy project reader that imports model/accessory references, camera/light/self-shadow tracks, model/bone/morph/accessory keyframes and core timeline state. PMMv1 resolves bone/morph tracks by loading referenced PMD/PMX model resources from the project path.
- PMMv2 legacy project writer and CLI export command for `.zmm`, `.nma` and `.pmm` inputs.
- Scene model/accessory instances with transform state.
- VMD motion sampler for bone, morph, camera, light, model, self-shadow and accessory tracks.
- Vertex/UV/Bone Morph evaluation with group/flip morph weight expansion.
- Runtime model pose evaluation and CPU BDEF/QDEF skinning foundation.
- Silk.NET OpenGL preview host using GLFW.
- Basic OpenGL mesh renderer for PMD/PMX and ASCII `.x` geometry.
- Animated OpenGL preview path that updates CPU-skinned PMD/PMX vertices from VMD sampling.
- Diffuse texture path propagation and OpenGL texture sampling for model/accessory materials.
- Sphere/toon texture path propagation with basic OpenGL sampling.
- Basic OpenGL material edge pass using normal-expanded back-face rendering.
- Basic alpha blending path for transparent materials and cull-disable support for double-sided materials.
- OpenGL mesh rendering can consume MME runtime pass state for depth, blend and cull behavior while using the built-in material shader.
- OpenGL has an effect shader program cache and can draw geometry and full-screen buffer passes through MME multi-pass execution plans with translated GLSL when compilation succeeds, including matrix, viewport size, material diffuse and material texture/sphere/toon semantic bindings; otherwise geometry passes fall back to the built-in shader.
- OpenGL effect execution can interpret MME clear commands for the active framebuffer, including `ClearSetColor`, `ClearSetDepth` and `Clear`.
- OpenGL effect execution can create runtime color/depth target textures from MME `RENDERCOLORTARGET`, `RENDERDEPTHSTENCILTARGET` and `OFFSCREENRENDERTARGET` parameters, bind them to an FBO from script commands, resolve common render-target aliases, route multiple color attachments with draw buffers, bind matching sampler uniforms to those textures, attach offscreen targets with dedicated depth textures, execute a first-pass offscreen prepass from default-effect draw plans, and load/apply external `.fx` files referenced by offscreen default-effect conditions with built-in fallback on missing or unsupported effects.
- Transparent batch depth ordering and a basic projected ground-shadow pass.
- VMD camera, light, model visibility and accessory samples can be applied to scene state during animated preview.
- CCD IK solving with per-link angle limit clamping is integrated into model pose evaluation.
- Motion timeline editing primitives for moving, scaling, copying and deleting selected or ranged keyframes.
- CLI inspection command for `.pmd`, `.pmx`, `.vmd`, `.nmd`, `.x`, `.fx`, `.zmm`, `.nma` and `.pmm`; PMMv2 export command for project files.

Full nanoem feature parity is a multi-stage implementation. The next stages should add:

1. Complete MME shader execution: shared render-target ownership, mipmap generation, animated/control parameters, richer external-effect interaction rules and broader HLSL translation, plus binary/compressed DirectX `.x` support.
2. GPU model buffers, PMD/PMX material rendering, toon/edge/ground-shadow passes.
3. Physics integration and skinning.
4. ImGui.NET editor UI on top of the OpenGL host.
5. Audio playback, image/video export and plugin hosting.

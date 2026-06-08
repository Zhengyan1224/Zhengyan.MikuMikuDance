namespace Zhengyan.MikuMikuDance.Core.Diagnostics;

public sealed record FeatureCatalog(
    IReadOnlyList<string> CoreFormats,
    IReadOnlyList<string> SceneObjects,
    IReadOnlyList<string> EditingSurfaces,
    IReadOnlyList<string> RuntimeSystems)
{
    public static FeatureCatalog FromNanoemReference()
    {
        return new FeatureCatalog(
            [
                "PMD model compatibility",
                "PMX model compatibility",
                "VMD motion compatibility",
                "NMD extended motion compatibility",
                "ZMM/NMA project compatibility",
                "PMM v1/v2 legacy project import and PMM v2 export",
                "DirectX .x accessory compatibility",
                "MME .fx effect structure, runtime metadata, multi-pass execution planning, basic GLSL translation and uniform metadata compatibility"
            ],
            [
                "Model",
                "Accessory",
                "Bone",
                "Morph",
                "Camera",
                "Light",
                "Self shadow",
                "Rigid body",
                "Joint",
                "Soft body"
            ],
            [
                "Timeline keyframe editing",
                "Viewport transform gizmo",
                "Model parameter editor",
                "Morph panel",
                "Accessory panel",
                "Camera and view panel",
                "Play panel",
                "Undo/redo",
                "Copy/paste and mirrored paste"
            ],
            [
                "OpenGL rendering",
                "MME pass-state binding",
                "MME geometry multi-pass rendering",
                "MME full-screen buffer pass rendering",
                "MME clear script command execution",
                "MME runtime render target and depth target binding",
                "MME render target alias and MRT draw-buffer routing",
                "MME offscreen target metadata and depth attachment setup",
                "MME offscreen default-effect draw-plan decisions",
                "MME offscreen prepass rendering",
                "MME offscreen external default-effect loading",
                "MME effect shader compile cache and semantic binding",
                "Texture and toon rendering",
                "Edge rendering",
                "Ground shadow",
                "Skinning",
                "Physics",
                "Audio playback",
                "Video/image export",
                "Plugin host"
            ]);
    }
}

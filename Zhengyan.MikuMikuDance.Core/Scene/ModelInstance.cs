using Zhengyan.MikuMikuDance.Core.Modeling;

namespace Zhengyan.MikuMikuDance.Core.Scene;

public sealed class ModelInstance
{
    public ModelInstance(MmdModel model, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        Name = string.IsNullOrWhiteSpace(name) ? model.Name : name;
    }

    public string Name { get; set; }

    public MmdModel Model { get; }

    public bool Visible { get; set; } = true;

    public SceneTransform Transform { get; } = new();
}

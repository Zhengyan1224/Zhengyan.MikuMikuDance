using Zhengyan.MikuMikuDance.Rendering.OpenGL;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class OpenGlTexturePathResolverTests
{
    [Fact]
    public void ResolvesFileUriToLocalPath()
    {
        var path = Path.GetFullPath(Path.Combine("assets", "background.png"));
        var uri = new Uri(path);

        var resolved = OpenGlTexturePathResolver.Resolve(uri.ToString(), baseDirectory: null);

        Assert.Equal(path, resolved);
    }

    [Fact]
    public void ResolvesRelativePathAgainstBaseDirectory()
    {
        var baseDirectory = Path.GetFullPath("project");
        var expected = Path.GetFullPath(Path.Combine(baseDirectory, "images", "background.png"));

        var resolved = OpenGlTexturePathResolver.Resolve(@"images\background.png", baseDirectory);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void RejectsNonFileUri()
    {
        Assert.Null(OpenGlTexturePathResolver.Resolve("https://example.invalid/background.png", baseDirectory: null));
    }
}

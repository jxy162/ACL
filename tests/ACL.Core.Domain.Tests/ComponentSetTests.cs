using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class ComponentSetTests
{
    [Fact]
    public void EmptySets_AreEquivalent()
    {
        Assert.True(ComponentSet.Vanilla.EquivalentTo(new ComponentSet(new Dictionary<ModLoaderKind, string>())));
        Assert.True(ComponentSet.Vanilla.IsVanilla);
    }

    [Fact]
    public void LoaderOrder_DoesNotAffectEquivalence()
    {
        ComponentSet a = new(new Dictionary<ModLoaderKind, string>
        {
            [ModLoaderKind.Forge] = "47.2.0",
            [ModLoaderKind.Fabric] = "0.15.0",
        });
        ComponentSet b = new(new Dictionary<ModLoaderKind, string>
        {
            [ModLoaderKind.Fabric] = "0.15.0",
            [ModLoaderKind.Forge] = "47.2.0",
        });

        Assert.True(a.EquivalentTo(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentLoaderVersion_IsNotEquivalent()
    {
        ComponentSet a = new(new Dictionary<ModLoaderKind, string> { [ModLoaderKind.Forge] = "47.2.0" });
        ComponentSet b = new(new Dictionary<ModLoaderKind, string> { [ModLoaderKind.Forge] = "47.3.0" });
        Assert.False(a.EquivalentTo(b));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void OptiFineDifference_IsDetected()
    {
        ComponentSet withOptiFine = new(
            new Dictionary<ModLoaderKind, string>(),
            new OptiFineRef("C8"));
        Assert.False(withOptiFine.EquivalentTo(ComponentSet.Vanilla));
        Assert.False(withOptiFine.IsVanilla);
    }

    [Fact]
    public void WithLoader_ReturnsNewSet_LeavingOriginalUntouched()
    {
        ComponentSet original = ComponentSet.Vanilla;
        ComponentSet updated = original.WithLoader(ModLoaderKind.Fabric, "0.15.0");

        Assert.True(original.IsVanilla);
        Assert.Equal("0.15.0", updated.Loaders[ModLoaderKind.Fabric]);
        Assert.True(original.EquivalentTo(ComponentSet.Vanilla));
    }
}
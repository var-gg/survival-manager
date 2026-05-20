using NUnit.Framework;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class EquipmentPresentationPolicyTests
{
    [TestCase("Common", "common", true)]
    [TestCase("Rare", "rare", true)]
    [TestCase("Epic", "epic", true)]
    [TestCase("Magic", "rare", false)]
    [TestCase("Legendary", "epic", false)]
    public void Build_MapsRarity_ToLaunchSurface(string rawRarity, string expectedRarity, bool expectedSupported)
    {
        var presentation = EquipmentPresentationPolicy.Build("weapon", "blade", rawRarity, "Baseline", new[] { "Reforge" });

        Assert.That(presentation.RarityKey, Is.EqualTo(expectedRarity));
        Assert.That(presentation.RawRarityKey, Is.EqualTo(rawRarity.ToLowerInvariant()));
        Assert.That(presentation.IsLaunchSupportedRarity, Is.EqualTo(expectedSupported));
    }

    [Test]
    public void Build_DoesNotTreatUniqueAsRarity()
    {
        var presentation = EquipmentPresentationPolicy.Build("weapon", "blade", "Epic", "Unique", new[] { "Reforge" });

        Assert.That(presentation.RarityKey, Is.EqualTo("epic"));
        Assert.That(presentation.IdentityKey, Is.EqualTo("unique"));
        Assert.That(presentation.IdentityLabel, Is.EqualTo("SIGNATURE"));
        Assert.That(presentation.ShowsIdentityBadge, Is.True);
    }

    [Test]
    public void Build_TreatsShieldAsWeaponFamily_NotSlot()
    {
        var presentation = EquipmentPresentationPolicy.Build("weapon", "shield", "Rare", "Baseline", new[] { "Reforge" });

        Assert.That(presentation.SlotKey, Is.EqualTo("weapon"));
        Assert.That(presentation.FamilyKey, Is.EqualTo("shield"));
    }

    [Test]
    public void Build_HidesWeaponFamily_ForNonWeaponSlot()
    {
        var presentation = EquipmentPresentationPolicy.Build("armor", "shield", "Rare", "Baseline", new[] { "Reforge" });

        Assert.That(presentation.SlotKey, Is.EqualTo("armor"));
        Assert.That(presentation.FamilyKey, Is.Empty);
        Assert.That(presentation.FamilyLabel, Is.Empty);
    }

    [Test]
    public void Build_TreatsEmptyCraftOperations_AsLaunchRefitCompatible()
    {
        var presentation = EquipmentPresentationPolicy.Build("weapon", "blade", "Rare", "Baseline", System.Array.Empty<string>());

        Assert.That(presentation.CanRefit, Is.True);
    }

    [Test]
    public void Build_DoesNotExposeDeferredCraftOperations_AsRefit()
    {
        var presentation = EquipmentPresentationPolicy.Build("weapon", "blade", "Rare", "Baseline", new[] { "Temper", "Seal", "Imprint", "Salvage" });

        Assert.That(presentation.CanRefit, Is.False);
    }
}

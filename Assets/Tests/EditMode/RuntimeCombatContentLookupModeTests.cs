using System;
using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class RuntimeCombatContentLookupModeTests
{
    [Test]
    public void RuntimeCombatContentLookup_DefaultsToResourcesOnlyMode()
    {
        var lookup = CreateLookup();

        Assert.That(lookup.AllowsEditorRecoveryFallback, Is.False);
    }

    [Test]
    public void RuntimeCombatContentLookup_CanOptIntoEditorRecoveryFallback()
    {
        var lookup = CreateLookup(allowEditorRecoveryFallback: true);

        Assert.That(lookup.AllowsEditorRecoveryFallback, Is.True);
    }

    private static RuntimeCombatContentLookup CreateLookup(bool allowEditorRecoveryFallback = false)
    {
        return (RuntimeCombatContentLookup)Activator.CreateInstance(
            typeof(RuntimeCombatContentLookup),
            new object[] { allowEditorRecoveryFallback })!;
    }
}

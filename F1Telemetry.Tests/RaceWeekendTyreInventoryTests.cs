using System.IO;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies structured race-weekend tyre inventory behavior.
/// </summary>
public sealed class RaceWeekendTyreInventoryTests
{
    /// <summary>
    /// Verifies legacy inventory text migrates into structured counts.
    /// </summary>
    [Fact]
    public void FromLegacyInventoryText_ParsesKnownCompounds()
    {
        var plan = RaceWeekendTyrePlan.FromLegacyInventoryText(
            "Soft=1; Medium=2; Hard=3; Intermediate=4; Wet=5",
            maxRecommendedWearPercent: 62);

        Assert.Equal(1, plan.SoftCount);
        Assert.Equal(2, plan.MediumCount);
        Assert.Equal(3, plan.HardCount);
        Assert.Equal(4, plan.IntermediateCount);
        Assert.Equal(5, plan.WetCount);
        Assert.Equal("Soft=1; Medium=2; Hard=3; Intermediate=4; Wet=5", plan.InventoryText);
    }

    /// <summary>
    /// Verifies malformed legacy inventory text falls back to zero counts.
    /// </summary>
    [Fact]
    public void FromLegacyInventoryText_InvalidText_DefaultsToZero()
    {
        var plan = RaceWeekendTyrePlan.FromLegacyInventoryText("Soft=nope; Medium=; broken");

        Assert.Equal(0, plan.SoftCount);
        Assert.Equal(0, plan.MediumCount);
        Assert.Equal(0, plan.HardCount);
        Assert.Equal(0, plan.IntermediateCount);
        Assert.Equal(0, plan.WetCount);
    }

    /// <summary>
    /// Verifies tyre inventory item decrement commands never go below zero.
    /// </summary>
    [Fact]
    public void InventoryItem_Decrement_DoesNotGoBelowZero()
    {
        var item = new RaceWeekendTyreInventoryItemViewModel(
            "Soft",
            "红胎",
            "Soft",
            "#E84855",
            () => { });

        item.DecrementCommand.Execute(null);

        Assert.Equal(0, item.Count);
        Assert.Equal("0", item.CountText);
    }

    /// <summary>
    /// Verifies the AI/TTS view no longer exposes the legacy free-form tyre inventory textbox.
    /// </summary>
    [Fact]
    public void AiTtsView_DoesNotBindLegacyInventoryTextBox()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "F1Telemetry.App", "Views", "AiTtsView.xaml"));

        Assert.DoesNotContain("RaceWeekendTyrePlanText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("inventoryText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Soft=0; Medium=0", xaml, StringComparison.Ordinal);
        Assert.Contains("RaceWeekendTyreInventoryItems", xaml, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}

using SystemCompanion.Models;
using SystemCompanion.Services;
using Xunit;

namespace SystemCompanion.Tests;

/// <summary>
/// The five named cases the advice must keep answering the same way. Each reads a
/// written snapshot so the suite runs without a window and without a network.
/// </summary>
public sealed class RecommendationEngineTests
{
    [Fact]
    public void A_balanced_power_plan_is_worth_doing()
    {
        var suggestion = TestSupport.Reading("balanced-power-plan.json").Advice().Find("power-plan");

        Assert.Equal(SuggestionState.WorthDoing, suggestion.State);
    }

    [Fact]
    public void Long_path_support_enabled_is_already_good()
    {
        var suggestion = TestSupport.Reading("long-paths-on.json").Advice().Find("long-paths");

        Assert.Equal(SuggestionState.AlreadyGood, suggestion.State);
    }

    [Fact]
    public void Four_sticks_running_at_6000_are_not_flagged()
    {
        var advice = TestSupport.Reading("four-sticks-6000.json").Advice();

        Assert.Equal(SuggestionState.AlreadyGood, advice.Find("bios-xmp").State);
        Assert.DoesNotContain(advice, suggestion => suggestion.State == SuggestionState.WorthDoing
                                                    && suggestion.Id is "bios-xmp" or "bios-4g" or "bios-resizable-bar");
    }

    [Fact]
    public void Memory_integrity_on_is_flagged_with_its_trade_off()
    {
        var suggestion = TestSupport.Reading("memory-integrity-on.json").Advice().Find("memory-integrity");

        Assert.Equal(SuggestionState.WorthDoing, suggestion.State);
        Assert.Contains("lowers protection", suggestion.Why, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_reading_with_no_graphics_adapter_degrades_to_an_explanation()
    {
        var advice = TestSupport.Reading("no-graphics.json").Advice();

        Assert.Equal("Unknown", advice.Find("gpu-vram").Current);
        Assert.Equal("Unknown", advice.Find("refresh-rate").Current);
        Assert.Equal(SuggestionState.CheckYourself, advice.Find("gpu-driver-fresh").State);
        Assert.Equal(SuggestionState.AlreadyGood, advice.Find("gpu-vram").State);
    }
}

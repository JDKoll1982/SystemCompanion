using SystemCompanion.Services;
using Xunit;

namespace SystemCompanion.Tests;

public sealed class FirmwareReferenceTests
{
    [Fact]
    public void A_matching_tag_passes()
    {
        Assert.True(FirmwareReference.Matches("E7E06IMS.AK0"));
    }

    [Fact]
    public void A_different_tag_fails()
    {
        Assert.False(FirmwareReference.Matches("E7E06IMS.ZZ9"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_tag_fails(string? tag)
    {
        Assert.False(FirmwareReference.Matches(tag));
    }
}

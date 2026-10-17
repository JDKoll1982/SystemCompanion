namespace SystemCompanion.Services;

/// <summary>
/// The firmware tag the rest of the data set is written against.
/// </summary>
public static class FirmwareReference
{
    public const string SupportedTag = "E7E06IMS.AK0";

    /// <summary>
    /// True when <paramref name="tag"/> names the supported firmware exactly, ignoring no case and tolerating no padding.
    /// </summary>
    public static bool Matches(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(tag, SupportedTag, StringComparison.Ordinal);
    }
}

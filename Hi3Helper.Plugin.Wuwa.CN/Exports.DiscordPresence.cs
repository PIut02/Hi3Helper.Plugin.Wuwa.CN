using Hi3Helper.Plugin.Core.DiscordPresence;

namespace Hi3Helper.Plugin.Wuwa.CN;

public partial class Exports
{
    private const ulong DiscordPresenceId = 1425682447546454016u;

    private const string DiscordPresenceLargeIconUrl =
        "https://play-lh.googleusercontent.com/g-47eiWo7LYLBFOQrYNzjrsTf4HRzUT--lSGrqJ9BPJFV72FEdYo5rSMI6AXBDyzrA";

    protected override bool GetCurrentDiscordPresenceInfoCore(
        DiscordPresenceExtension.DiscordPresenceContext context,
        out ulong presenceId,
        out string? largeIconUrl,
        out string? largeIconTooltip,
        out string? smallIconUrl,
        out string? smallIconTooltip)
    {
        presenceId = DiscordPresenceId;
        largeIconUrl = DiscordPresenceLargeIconUrl;
        largeIconTooltip = null;
        smallIconUrl = null;
        smallIconTooltip = null;

        return true;
    }
}
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace RememberAskingPrice
{
    internal class Service
    {
        internal static RememberAskingPrice Plugin { get; set; } = null!;

        internal static ConfigurationV1 Configuration { get; set; } = null!;

        [PluginService]
        internal static DalamudPluginInterface Interface { get; private set; } = null!;

        [PluginService]
        internal static SigScanner Scanner { get; private set; } = null!;
    }
}

using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace RememberAskingPrice
{
    internal class Service
    {
        internal static RememberAskingPrice Plugin { get; set; } = null!;

        internal static ConfigurationV1 Configuration { get; set; } = null!;

        [PluginService]
        internal static IDalamudPluginInterface Interface { get; private set; } = null!;

        [PluginService]
        internal static IGameInteropProvider InteropProvider { get; private set; } = null!;

        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;
    }
}

using System;
using System.IO;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RememberAskingPrice
{
    internal delegate IntPtr OnSetupDelegate(IntPtr addon, uint a2, IntPtr dataPtr);
    internal unsafe delegate IntPtr ReceiveEventDelegate(void* eventListener, EventType evt, uint which, void* eventData, void* inputData);

    internal class RememberAskingPrice : IDalamudPlugin
    {
        public string Name => "RememberAskingPrice";

        private readonly WindowSystem windowSystem;
        private readonly PluginUI pluginUi;

        private string? OpenItem = null;

        private Hook<OnSetupDelegate> AddonRetainerSellOnSetupHook = null!;
        private Hook<ReceiveEventDelegate> AddonRetainerSellReceiveEventHook = null!;

        public RememberAskingPrice(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Service.Plugin = this;

            var pluginConfigPath = new FileInfo(Path.Combine(Service.Interface.ConfigDirectory.Parent!.FullName, $"RememberAskingPrice.json"));
            Service.Configuration = ConfigurationV1.Load(pluginConfigPath) ?? new ConfigurationV1();

            this.AddonRetainerSellOnSetupHook = Service.InteropProvider.HookFromSignature<OnSetupDelegate>("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 50 4C 89 74 24 ??", this.AddonRetainerSellOnSetupDetour);
            unsafe
            {
                this.AddonRetainerSellReceiveEventHook = Service.InteropProvider.HookFromSignature<ReceiveEventDelegate>("48 89 7C 24 ?? 4C 89 74 24 ?? 55 48 8B EC 48 83 EC 40 49 8B F9 4C 8B F1", this.AddonRetainerSellReceiveEventDetour);
            }

            this.AddonRetainerSellOnSetupHook.Enable();
            this.AddonRetainerSellReceiveEventHook.Enable();

            this.pluginUi = new();
            this.windowSystem = new(this.Name);
            this.windowSystem.AddWindow(this.pluginUi);
            Service.Interface.UiBuilder.Draw += this.windowSystem.Draw;
            Service.Interface.UiBuilder.OpenConfigUi += this.OnOpenConfigUi;
        }

        public void Dispose()
        {
            Service.Interface.UiBuilder.OpenConfigUi -= this.OnOpenConfigUi;
            Service.Interface.UiBuilder.Draw -= this.windowSystem.Draw;

            this.AddonRetainerSellOnSetupHook.Dispose();
            this.AddonRetainerSellReceiveEventHook.Dispose();
        }

        internal void OnOpenConfigUi() => this.pluginUi.Toggle();

        private SeString GetSeString(IntPtr textPtr)
        {
            return MemoryHelper.ReadSeStringNullTerminated(textPtr);
        }

        private string GetSeStringText(SeString sestring)
        {
            var pieces = sestring.Payloads.OfType<TextPayload>().Select(t => t.Text);
            var text = string.Join(string.Empty, pieces).Replace('\n', ' ').Trim();
            return text;
        }

        unsafe private IntPtr AddonRetainerSellOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            Service.PluginLog.Debug("EnableComplementSellPrice::AddonRetainerSellOnSetupDetour");
            var result = this.AddonRetainerSellOnSetupHook!.Original(addon, a2, dataPtr);

            try
            {
                if (!Service.Configuration.Enabled)
                    return result;

                var _addon = (AddonRetainerSell*)addon;
                var itemname = GetSeStringText(GetSeString((IntPtr)_addon->ItemName->NodeText.StringPtr));

                Service.PluginLog.Debug($"ItemName = {itemname}");

                this.OpenItem = itemname;

                if (!Service.Configuration.Data.TryGetValue(this.OpenItem, out var savedData))
                {
                    return result;
                }

                if (Service.Configuration.EnabledAskingPrice && savedData.AskingPrice > 0)
                {
                    Service.PluginLog.Debug($"Restore Item = {this.OpenItem} Price = {savedData.AskingPrice}");
                    _addon->AskingPrice->SetValue((int)savedData.AskingPrice);
                }

                if (Service.Configuration.EnabledQuantity && savedData.Quantity > 0)
                {
                    Service.PluginLog.Debug($"Restore Item = {this.OpenItem} Quantity = {savedData.Quantity}");
                    _addon->Quantity->SetValue((int)savedData.Quantity);
                }

            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }

        unsafe private IntPtr AddonRetainerSellReceiveEventDetour(void* eventListener, EventType evt, uint which, void* eventData, void* inputData)
        {
            Service.PluginLog.Debug("EnableComplementSellPrice::AddonRetainerSellReceiveEvent");

            var result = this.AddonRetainerSellReceiveEventHook.Original(eventListener, evt, which, eventData, inputData);

            try
            {
                if (!Service.Configuration.Enabled)
                    return result;

                if ((evt == EventType.CHANGE) && which == 0x15)
                {
                    // Clicked Confirm
                    var _addon = (AddonRetainerSell*)eventListener;

                    // AskingPrice
                    if (Service.Configuration.EnabledAskingPrice)
                    {
                        var askingPriceText = _addon->AskingPrice->AtkComponentInputBase.AtkTextNode->NodeText.ToString();
                        if (uint.TryParse(askingPriceText, out var askingPrice) && !string.IsNullOrEmpty(this.OpenItem))
                        {
                            Service.Configuration.SetAskingPrice(this.OpenItem, (uint)askingPrice);
                            Service.Configuration.Save();
                            Service.PluginLog.Debug($"Set LastSetPrices[{this.OpenItem}] = {askingPrice}");

                        }
                    }

                    // Quantity
                    if (Service.Configuration.EnabledQuantity)
                    {
                        var quantityText = _addon->Quantity->AtkComponentInputBase.AtkTextNode->NodeText.ToString();
                        if (uint.TryParse(quantityText, out var quantity) && !string.IsNullOrEmpty(this.OpenItem))
                        {
                            Service.Configuration.SetQuantity(this.OpenItem, (uint)quantity);
                            Service.Configuration.Save();
                            Service.PluginLog.Debug($"Set LastSetQuantity[{this.OpenItem}] = {quantity}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }
    }
}

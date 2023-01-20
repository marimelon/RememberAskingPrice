using System;
using System.IO;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
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

        public RememberAskingPrice(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Service.Plugin = this;

            var pluginConfigPath = new FileInfo(Path.Combine(Service.Interface.ConfigDirectory.Parent!.FullName, $"RememberAskingPrice.json"));
            Service.Configuration = ConfigurationV1.Load(pluginConfigPath) ?? new ConfigurationV1();

            if (Service.Scanner.TryScanText("48 89 5C 24 ?? 55 56 57 48 83 EC 50 4C 89 64 24 ??", out var ptr1))
            {
                this.AddonRetainerSellOnSetupHook = Hook<OnSetupDelegate>.FromAddress(ptr1, this.AddonRetainerSellOnSetupDetour);
            }

            if (Service.Scanner.TryScanText("40 53 48 83 EC 20 0F B7 C2 48 8B D9 83 F8 17", out var ptr2))
            {
                unsafe
                {
                    this.AddonRetainerSellReceiveEventHook = Hook<ReceiveEventDelegate>.FromAddress(ptr2, this.AddonRetainerSellReceiveEventDetour);
                }
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
            PluginLog.Debug("EnableComplementSellPrice::AddonRetainerSellOnSetupDetour");
            var result = this.AddonRetainerSellOnSetupHook!.Original(addon, a2, dataPtr);

            try
            {
                if (!Service.Configuration.Enabled)
                    return result;

                var _addon = (AddonRetainerSell*)addon;
                var itemname = GetSeStringText(GetSeString((IntPtr)_addon->ItemName->NodeText.StringPtr));

                PluginLog.Debug($"ItemName = {itemname}");

                this.OpenItem = itemname;

                if (!Service.Configuration.Data.TryGetValue(this.OpenItem, out var savedData))
                {
                    return result;
                }

                if (Service.Configuration.EnabledAskingPrice && savedData.AskingPrice > 0)
                {
                    PluginLog.Debug($"Restore Item = {this.OpenItem} Price = {savedData.AskingPrice}");
                    _addon->AskingPrice->SetValue((int)savedData.AskingPrice);
                }

                if (Service.Configuration.EnabledQuantity && savedData.Quantity > 0)
                {
                    PluginLog.Debug($"Restore Item = {this.OpenItem} Quantity = {savedData.Quantity}");
                    _addon->Quantity->SetValue((int)savedData.Quantity);
                }

            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }

        unsafe private IntPtr AddonRetainerSellReceiveEventDetour(void* eventListener, EventType evt, uint which, void* eventData, void* inputData)
        {
            PluginLog.Debug("EnableComplementSellPrice::AddonRetainerSellReceiveEvent");

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
                            PluginLog.Debug($"Set LastSetPrices[{this.OpenItem}] = {askingPrice}");

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
                            PluginLog.Debug($"Set LastSetQuantity[{this.OpenItem}] = {quantity}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }
    }
}

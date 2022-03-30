using System;
using System.Linq;
using System.Runtime.InteropServices;
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

        // [Signature("48 89 5C 24 ?? 55 56 57 48 83 EC 50 4C 89 64 24 ??", DetourName = nameof(AddonRetainerSellOnSetupDetour))]
        private Hook<OnSetupDelegate>? AddonRetainerSellOnSetupHook { get; init; }

        // [Signature("40 53 48 83 EC 20 0F B7 C2 48 8B D9 83 F8 17", DetourName = nameof(AddonRetainerSellReceiveEventDetour))]
        private Hook<ReceiveEventDelegate>? AddonRetainerSellReceiveEventHook { get; init; }

        public RememberAskingPrice(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Service.Plugin = this;

            Service.Configuration = (Configuration?)Service.Interface.GetPluginConfig() ?? new Configuration();

            if (Service.Scanner.TryScanText("48 89 5C 24 ?? 55 56 57 48 83 EC 50 4C 89 64 24 ??", out var ptr1))
            {
                this.AddonRetainerSellOnSetupHook = new Hook<OnSetupDelegate>(ptr1, this.AddonRetainerSellOnSetupDetour);
            }

            if (Service.Scanner.TryScanText("40 53 48 83 EC 20 0F B7 C2 48 8B D9 83 F8 17", out var ptr2))
            {
                unsafe
                {
                    this.AddonRetainerSellReceiveEventHook = new Hook<ReceiveEventDelegate>(ptr2, this.AddonRetainerSellReceiveEventDetour);
                }
            }

            this.AddonRetainerSellOnSetupHook?.Enable();
            this.AddonRetainerSellReceiveEventHook?.Enable();

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
            
            this.AddonRetainerSellOnSetupHook?.Dispose();
            this.AddonRetainerSellReceiveEventHook?.Dispose();
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

                if (Service.Configuration.Data.TryGetValue(this.OpenItem, out uint price))
                {
                    PluginLog.Debug($"Restore Item = {this.OpenItem} Price = {price}");
                    SetRetainerSellValue(price, _addon);
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

            var result = this.AddonRetainerSellReceiveEventHook!.Original(eventListener, evt, which, eventData, inputData);

            try
            {
                if (!Service.Configuration.Enabled)
                    return result;

                if ((evt == EventType.CHANGE) && which == 0x15)
                {
                    // Clicked Confirm
                    var _addon = (AddonRetainerSell*)eventListener;
                    var askingPriceText = GetSeStringText(GetSeString((IntPtr)_addon->AskingPrice->AtkTextNode->NodeText.StringPtr));

                    if (uint.TryParse(askingPriceText, out var askingPrice) && !string.IsNullOrEmpty(this.OpenItem))
                    {
                        Service.Configuration.Data[this.OpenItem] = askingPrice;
                        Service.Configuration.Save();
                        PluginLog.Debug($"Set LastSetPrices[{this.OpenItem}] = {askingPrice}");

                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }


        unsafe private void SetRetainerSellValue(uint value, AddonRetainerSell* addon)
        {
            try
            {
                var addonBase = &addon->AtkUnitBase;
                var target = addon->AskingPrice;
                var listener = &addonBase->AtkEventListener;

                var eventData = stackalloc void*[3];
                eventData[0] = null;
                eventData[1] = target->AtkComponentInputBase.AtkComponentBase.OwnerNode;
                eventData[2] = addonBase;

                var inputDataLength = 8;
                var inputData = stackalloc void*[inputDataLength];
                for (var i = 0; i < inputDataLength; i++)
                {
                    inputData[i] = null;
                }

                inputData[0] = (void*)value;

                var eventType = EventType.SLIDER_CHANGE;

                var receiveEventAddress = new IntPtr(listener->vfunc[2]);
                var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;
                receiveEvent(listener, eventType, 0, eventData, inputData);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace RememberAskingPrice
{
    [Serializable]
    internal class Configuration : IPluginConfiguration
    {
        public void Save() => Service.Interface.SavePluginConfig(this);

        public int Version { get; set; } = 0;

        public bool Enabled { get; set; } = true;

        public Dictionary<string, uint> Data { get; set; } = new();
    }
}

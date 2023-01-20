using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace RememberAskingPrice
{
    [Serializable]
    internal class Configuration : IPluginConfiguration
    {
        public void Save() => Service.Interface.SavePluginConfig(this);

        public int Version { get; set; } = 0;

        public bool Enabled { get; set; } = true;

        public Dictionary<string, uint> Data { get; set; } = new();

        public static Configuration? Load(FileInfo pluginConfigPath)
        {
            if (!pluginConfigPath.Exists)
            {
                return null;
            }

            var data = File.ReadAllText(pluginConfigPath.FullName);
            dynamic? conf = JsonConvert.DeserializeObject(data);
            if (conf == null)
            {
                return new Configuration();
            }

            if ((int)conf.Version == 0)
            {
                return JsonConvert.DeserializeObject<Configuration>(data);
            }

            return null;
        }
    }

    [Serializable]
    internal class ConfigurationV1 : IPluginConfiguration
    {
        public void Save() => Service.Interface.SavePluginConfig(this);

        public int Version { get; set; } = 1;

        public bool Enabled { get; set; } = true;

        public bool EnabledAskingPrice { get; set; } = true;
        public bool EnabledQuantity { get; set; } = true;

        public Dictionary<string, (uint AskingPrice, uint Quantity)> Data { get; set; } = new();

        public static ConfigurationV1? Load(FileInfo pluginConfigPath)
        {
            if (!pluginConfigPath.Exists)
            {
                return null;
            }

            var data = File.ReadAllText(pluginConfigPath.FullName);
            dynamic? conf = JsonConvert.DeserializeObject(data);
            if (conf == null)
            {
                return new ConfigurationV1();
            }

            if ((int)conf.Version < 1)
            {
                var old = Configuration.Load(pluginConfigPath);
                if (old == null)
                {
                    return null;
                }

                return Migration(old);
            }

            if ((int)conf.Version == 1)
            {
                return JsonConvert.DeserializeObject<ConfigurationV1>(data);
            }

            return null;
        }

        public static ConfigurationV1 Migration(Configuration conf)
        {
            var v0 = (Configuration)conf;
            var v1 = new ConfigurationV1();
            v1.Enabled = v0.Enabled;
            foreach (var (key, value) in v0.Data)
            {
                v1.Data[key] = (value, 0);
            }
            PluginLog.Debug("Successfully migrated: v0 to v1");
            return v1;
        }

        public uint GetAskingPrice(string itemName)
        {
            if (this.Data.TryGetValue(itemName, out var result))
            {
                return result.AskingPrice;
            }
            return 0;
        }

        public uint GetQuantity(string itemName)
        {
            if (this.Data.TryGetValue(itemName, out var result))
            {
                return result.Quantity;
            }
            return 0;
        }

        public void SetAskingPrice(string itemName, uint price)
        {
            if (this.Data.ContainsKey(itemName))
            {
                this.Data[itemName] = (price, this.Data[itemName].Quantity);
            }
            else
            {
                this.Data[itemName] = (price, 0);
            }
        }

        public void SetQuantity(string itemName, uint quantity)
        {
            if (this.Data.ContainsKey(itemName))
            {
                this.Data[itemName] = (this.Data[itemName].AskingPrice, quantity);
            }
            else
            {
                this.Data[itemName] = (0, quantity);
            }
        }
    }


}

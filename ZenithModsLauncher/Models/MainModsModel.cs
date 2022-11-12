using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace ZenithModsLauncher.Models
{
    public class MainModsModel
    {
        public int ConfigVersion { get; set; }
        public bool EnableMods { get; set; }
        public Dictionary<string, Mod> Mods { get; set; }
    }

    public class Mod
    {
        public string HumanName { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, ModSettings> Settings { get; set; }
        [JsonProperty("IsPersistent", NullValueHandling = NullValueHandling.Ignore)]
        private bool? IsPersistent2 { get; set; }
        [JsonIgnore]
        public bool IsPersistent => IsPersistent2.HasValue && IsPersistent2.Value;
    }

    public class ModSettings
    {
        public string HumanName { get; set; }
        public string Description { get; set; }
        public ModSettingsType Type { get; set; }
        public JToken Value { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModSettingsType
    {
        /// <summary>
        /// Boolean setting (true/false)
        /// </summary>
        B,
        /// <summary>
        /// Int32 setting (-2147483648...2147483647)
        /// </summary>
        I,
        /// <summary>
        /// String setting ("abcd")
        /// </summary>
        S,
        /// <summary>
        /// Color setting ("FFFFFFFF")
        /// </summary>
        C
    }
}

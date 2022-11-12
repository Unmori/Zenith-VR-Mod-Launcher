using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ZenithModsLauncher.Models
{
    public class ChangeLogVersion
    {
        [JsonProperty("ver")]
        public string Version { get; set; }

        [JsonProperty("data")]
        public string Description { get; set; }

        [JsonIgnore]
        public List<string> Entries => Description?.Replace("\r", "").Split('\n').ToList();
    }
}

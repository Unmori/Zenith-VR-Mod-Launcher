using Newtonsoft.Json;
using System.Collections.Generic;

namespace ZenithModsLauncher.Models
{
    public class DirectorySteam
    {
        public string path { get; set; }
        public string label { get; set; }
        public string contentid { get; set; }
        public string totalsize { get; set; }
        public string update_clean_bytes_tally { get; set; }
        public string time_last_update_corruption { get; set; }
        public Dictionary<string, object> apps { get; set; }
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ZenithModsLauncher.Models
{
    public class ChangeLogsModel
    {
        public List<ChangeLogVersion> ModsVersions { get; set; }
        public List<ChangeLogVersion> LauncherVersions { get; set; }

        public ChangeLogsModel(string launcherLogsResourcePath)
        {
            LauncherVersions = GetLauncherLogs(launcherLogsResourcePath);
        }

        private List<ChangeLogVersion> GetLauncherLogs(string launcherLogsResourcePath)
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(launcherLogsResourcePath))
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;

                var launcherLogs = JsonConvert
                    .DeserializeObject<List<ChangeLogVersion>>(
                    new StreamReader(ms).ReadToEnd());

                return launcherLogs.ToList();
            }
        }
    }
}

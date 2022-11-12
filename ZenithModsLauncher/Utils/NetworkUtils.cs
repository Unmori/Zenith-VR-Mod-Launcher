using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace ZenithModsLauncher.Utils
{
    public static class NetworkUtils
    {
        public static async Task<string> GetFile(string uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.Method = "GET";
            using var response = await request.GetResponseAsync();
            using var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();
        }

        public static async Task GetFile(string uri, Func<Stream, long, Task> callback)
        {
            var request = WebRequest.CreateHttp(uri);
            request.Method = "GET";
            using var response = await request.GetResponseAsync();
            var length = response.ContentLength;
            using var stream = response.GetResponseStream();
            await callback(stream, length);
        }
    }
}

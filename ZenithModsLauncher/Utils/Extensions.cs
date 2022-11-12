using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZenithModsLauncher.Utils
{
    internal static class Extensions
    {
        public static async Task CopyToAsync(this Stream source, Stream destination,
            IProgress<long> progress, CancellationToken cancellationToken = default,
            int bufferSize = 0x1000)
        {
            var buffer = new byte[bufferSize];
            int bytesRead;
            long totalRead = 0;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                totalRead += bytesRead;
                progress.Report(totalRead);
            }
        }

        public static DependencyObject GetScrollViewer(this DependencyObject o)
        {
            if (o is ScrollViewer) return o;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);

                var result = GetScrollViewer(child);
                if (result == null) continue;
                else return result;
            }

            return null;
        }
    }
}

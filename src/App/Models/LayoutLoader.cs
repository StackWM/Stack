namespace LostTech.Stack.Models
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using System.Xml;
    using LostTech.Stack.InternalExtensions;
    using LostTech.Stack.Zones;
    using MahApps.Metro.Controls;
    using PCLStorage;
    using FileAccess = PCLStorage.FileAccess;

    public class LayoutLoader
    {
        readonly StringBuilder problems = new StringBuilder();
        readonly IFolder layoutDirectory;
        readonly Stopwatch loadTimer = new Stopwatch();

        public LayoutLoader(IFolder layoutDirectory) {
            this.layoutDirectory = layoutDirectory ?? throw new ArgumentNullException(nameof(layoutDirectory));
        }

        public async Task<FrameworkElement> LoadLayoutOrDefault(string fileName)
        {
            // TODO: SEC: untrusted XAML https://msdn.microsoft.com/en-us/library/ee856646(v=vs.110).aspx

            if (this.layoutDirectory == null)
                throw new ArgumentNullException(nameof(this.layoutDirectory));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            this.loadTimer.Restart();

            if (Path.GetInvalidFileNameChars().Any(fileName.Contains))
                throw new ArgumentException();

            var file = await this.layoutDirectory.GetFileOrNull(fileName);
            if (file == null) {
                Debug.WriteLine($"layout {fileName} was not found. loading default");
                return MakeDefaultLayout();
            }

            FrameworkElement layout;
            using (var stream = await file.OpenAsync(FileAccess.Read))
            using (var xmlReader = XmlReader.Create(stream)) {
                try {
                    layout = (FrameworkElement)XamlReader.Load(xmlReader);
                } catch (XamlParseException e) {
                    this.problems.AppendLine($"{file.Name}: {e.Message}");
                    return MakeDefaultLayout();
                }
            }

            foreach (string zoneProblem in layout.FindChildren<Control>()
                .OfType<IObjectWithProblems>()
                .SelectMany(zone => zone.Problems))
                this.problems.AppendLine($"{file.Name}: {zoneProblem}");

            Debug.WriteLine($"loaded layout {fileName} in {this.loadTimer.ElapsedMilliseconds}ms");
            return layout;
        }

        public string Problems => this.problems.ToString();

        public static FrameworkElement MakeDefaultLayout() => new Grid {
            Children = {
                new Zone {},
            }
        };
    }
}

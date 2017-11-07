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
    using PCLStorage;
    using FileAccess = PCLStorage.FileAccess;

    public class LayoutLoader
    {
        readonly StringBuilder problems = new StringBuilder();
        readonly IFolder layoutDirectory;

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


            if (Path.GetInvalidFileNameChars().Any(fileName.Contains))
                throw new ArgumentException();

            var file = await this.layoutDirectory.GetFileOrNull(fileName);
            if (file == null) {
                Debug.WriteLine($"layout {fileName} was not found. loading default");
                return MakeDefaultLayout();
            }

            using (var stream = await file.OpenAsync(FileAccess.Read))
            using (var xmlReader = XmlReader.Create(stream)) {
                try {
                    var layout = (FrameworkElement)XamlReader.Load(xmlReader);
                    Debug.WriteLine($"loaded layout {fileName}");
                    return layout;
                } catch (XamlParseException e) {
                    this.problems.AppendLine($"{file.Name}: {e.Message}");
                    return MakeDefaultLayout();
                }
            }
        }

        public string Problems => this.problems.ToString();

        public static FrameworkElement MakeDefaultLayout() => new Grid {
            Children = {
                new Zone {},
            }
        };
    }
}

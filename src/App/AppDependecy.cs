namespace LostTech.Stack
{
    using System;
    using System.Linq;

    public class AppDependecy
    {
        public AppDependecy(string name, string uri = null, string license = null)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Authors = $"contributors to {this.Name}";
            if (uri != null)
                this.Uri = new Uri(uri);
            this.License = license;
        }

        public string Name { get; set; }
        public string Authors { get; set; }
        public string License { get; set; }
        public Uri Uri { get; set; }

        public static AppDependecy[] Dependencies { get; } = new[]{
            new AppDependecy(nameof(PCLStorage)){ Uri = new Uri("https://github.com/dsplaisted/PCLStorage"), License = "MS-PL"},
            new AppDependecy(nameof(Validation)){Uri = new Uri("https://github.com/aarnott/Validation"), License = "MS-PL"},
            new AppDependecy(nameof(Microsoft.HockeyApp)){Uri = new Uri("https://hockeyapp.net/"), License = "MIT"},
            new AppDependecy("JetBrains.Annotations", uri: "https://www.nuget.org/packages/JetBrains.Annotations", license: "MIT"),
            new AppDependecy(nameof(PInvoke), uri: "https://github.com/AArnott/pinvoke", license: "MIT"),
            new AppDependecy(typeof(MahApps.Metro.AppTheme).Namespace, uri: "http://mahapps.com/", license: "MIT/MS-PL"),
            new AppDependecy(nameof(Prism), uri: "https://github.com/PrismLibrary/Prism", license: "Apache 2.0"),
            new AppDependecy(nameof(ValueConverters), uri: "https://github.com/thomasgalliker/ValueConverters.NET", license: "Apache 2.0"),
            new AppDependecy(nameof(EventHook), uri: "https://github.com/justcoding121/Windows-User-Action-Hook", license: "MIT"),
            new AppDependecy("Nito.AsyncEx", uri: "https://github.com/StephenCleary/AsyncEx", license: "MIT"),
            new AppDependecy("DesktopBridge.Helpers", uri: "https://github.com/qmatteoq/DesktopBridgeHelpers", license: "MIT"),
            new AppDependecy(nameof(CalcBinding), uri: "https://github.com/Alex141/CalcBinding", license: "Apache 2.0"),
        }.OrderBy(dependency => dependency.Name).ToArray();
    }
}

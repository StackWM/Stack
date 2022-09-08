namespace LostTech.Stack;

using System;
using System.Linq;

using LostTech.App.WPF;

using WindowsDesktop;

public class AppDependecy {
    public static LostTech.App.AppDependecy[] Dependencies { get; } = new LostTech.App.AppDependecy[]{
        new (nameof(Validation)){Uri = new Uri("https://github.com/aarnott/Validation"), License = "MS-PL"},
        new (nameof(Microsoft.AppCenter)){Uri = new Uri("https://visualstudio.microsoft.com/app-center/"), License = "MIT"},
        new ("JetBrains.Annotations", uri: "https://www.nuget.org/packages/JetBrains.Annotations", license: "MIT"),
        new (nameof(PInvoke), uri: "https://github.com/AArnott/pinvoke", license: "MIT"),
        new (typeof(MahApps.Metro.AppTheme).Namespace, uri: "http://mahapps.com/", license: "MIT/MS-PL"),
        new (nameof(Prism), uri: "https://github.com/PrismLibrary/Prism", license: "Apache 2.0"),
        new (nameof(ValueConverters), uri: "https://github.com/thomasgalliker/ValueConverters.NET", license: "Apache 2.0"),
        new (nameof(EventHook), uri: "https://github.com/justcoding121/Windows-User-Action-Hook", license: "MIT"),
        new ("DesktopBridge.Helpers", uri: "https://github.com/qmatteoq/DesktopBridgeHelpers", license: "MIT"),
        new (nameof(VirtualDesktop), uri: "https://github.com/Grabacr07/VirtualDesktop", license: "MIT"),
        new (nameof(CalcBinding), uri: "https://github.com/Alex141/CalcBinding", license: "Apache 2.0"),
    }
        .Concat(WpfAppDependency.Dependencies)
        .DistinctBy(d => d.Name)
        .OrderBy(dependency => dependency.Name)
        .ToArray();
}

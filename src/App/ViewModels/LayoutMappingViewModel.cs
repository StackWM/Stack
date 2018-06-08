namespace LostTech.Stack.ViewModels {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using JetBrains.Annotations;

    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.WindowManagement;
    using LostTech.Windows;

    class LayoutMappingViewModel : ILayoutMappingViewModel
    {
        readonly ScreenLayouts layoutMap;
        readonly IEnumerable<string> layoutNames;
        readonly LayoutLoader layoutLoader;
        readonly IScreenProvider screenProvider;

        public LayoutMappingViewModel(
                [NotNull] ScreenLayouts layoutMap,
                [NotNull] IEnumerable<string> layoutNames,
                [NotNull] LayoutLoader layoutLoader, 
                [NotNull] IScreenProvider screenProvider) {
            this.layoutMap = layoutMap ?? throw new ArgumentNullException(nameof(layoutMap));
            this.layoutNames = layoutNames ?? throw new ArgumentNullException(nameof(layoutNames));
            this.layoutLoader = layoutLoader ?? throw new ArgumentNullException(nameof(layoutLoader));
            this.screenProvider = screenProvider ?? throw new ArgumentNullException(nameof(screenProvider));
        }

        public ScreenLayoutSelector ShowLayoutSelector(Win32Screen screen) {
            string defaultOption = this.layoutMap.GetPreferredLayout(screen)
                                   ?? this.GetSuggestedLayout(screen);
            if (defaultOption == "Small Horizontal Left.xaml"
                || defaultOption == "Small Horizontal Right.xaml")
                defaultOption = "OOB Horizontal.xaml";
            defaultOption = Path.GetFileNameWithoutExtension(defaultOption);
            this.layoutMap.SetPreferredLayout(screen, fileName: $"{defaultOption}.xaml");
            var selectorViewModel = new LayoutSelectorViewModel {
                Layouts = this.layoutNames,
                Screen = screen,
                ScreenName = ScreenLayouts.GetDesignation(screen),
                Selected = defaultOption,
                Settings = this.layoutMap,
            };
            var selector = new ScreenLayoutSelector {
                LayoutLoader = this.layoutLoader,
                DataContext = selectorViewModel,
            };
            selector.Show();
            Position(screen, selector);

            return selector;
        }

        async static void Position(Win32Screen screen, ScreenLayoutSelector selector) {
            selector.TryGetNativeWindow()?.BringToFront().ReportAsWarning();
            await selector.FitToMargin(screen);
            selector.UpdateLayout();
            selector.ScrollToSelection();
        }

        public string GetPreferredLayoutFileName(Win32Screen screen) {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));

            return this.layoutMap.GetPreferredLayout(screen)
                   ?? $"{this.GetSuggestedLayout(screen)}.xaml";
        }

        string GetSuggestedLayout(Win32Screen screen) {
            if (!screen.WorkingArea.IsHorizontal())
                return "V Top+Rest";

            string[] screens = this.screenProvider.Screens
                .Where(ScreenExtensions.IsValidScreen)
                .OrderBy(s => s.WorkingArea.Left)
                .Select(s => s.ID).ToArray();
            bool isOnTheRight = screens.Length > 1 && screens.Last() == screen.ID;
            bool isBig = screen.TransformFromDevice.Transform(screen.WorkingArea.Size.AsWPFVector()).X > 2000;
            bool isWide = screen.WorkingArea.Width > 2.1 * screen.WorkingArea.Height;
            string leftOrRight = isOnTheRight ? "Right" : "Left";
            string kind = isWide ? "Wide" : isBig ? "Large Horizontal" : "Small Horizontal";
            return $"{kind} {leftOrRight}";
        }
    }
}

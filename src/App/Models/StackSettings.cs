namespace LostTech.Stack.Models
{
    using System;
    using PCLStorage;
    using System.Threading.Tasks;

    class StackSettings
    {
        readonly IFolder localSettings;

        public ScreenLayouts LayoutMap { get; private set; }

        private StackSettings(IFolder localSettings)
        {
            this.localSettings = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
        }

        public static async Task<StackSettings> Load(IFolder localSettings = null)
        {
            localSettings = localSettings ?? FileSystem.Current.LocalStorage;

            var result = new StackSettings(localSettings)
            {
                LayoutMap = await Serializer.Deserialize<ScreenLayouts>(localSettings, "LayoutMap.xml").ConfigureAwait(false) ?? new ScreenLayouts(),
            };
            return result;
        }
    }
}

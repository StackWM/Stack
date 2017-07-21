namespace LostTech.Stack.Settings
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Extensibility.Filters;
    using LostTech.Stack.Models;
    using PCLStorage;
    using LegacyWindowGroup = Models.Legacy.WindowGroup;

    class SettingsMigration
    {
        public static async Task<bool> Migrate([NotNull] IFolder localSettingsFolder) {
            if (localSettingsFolder == null)
                throw new ArgumentNullException(nameof(localSettingsFolder));

            var localSettings = XmlSettings.Create(localSettingsFolder);

            SettingsSet<CopyableObservableCollection<WindowGroup>, CopyableObservableCollection<WindowGroup>>
                windowGroups;
            try {
                windowGroups = await localSettings
                    .Load<CopyableObservableCollection<WindowGroup>>("WindowGroups.xml").ConfigureAwait(false);
            } catch (Exception) {
                return false;
            }

            if (windowGroups == null)
                return false;

            if (windowGroups.Value.All(group => group.VersionSetExplicitly && group.Version == WindowGroup.LatestVersion))
                return false;

            var legacySettings = XmlSettings.Create(localSettingsFolder);
            var legacyGroups = await legacySettings
                .Load<CopyableObservableCollection<LegacyWindowGroup>>("WindowGroups.xml").ConfigureAwait(false);

            windowGroups.Value.Clear();

            foreach (var legacyGroup in legacyGroups.Value) {
                WindowGroup group = Migrate(legacyGroup);
                windowGroups.Value.Add(group);
            }

            localSettings.ScheduleSave();
            await Task.WhenAll(legacySettings.DisposeAsync(), localSettings.DisposeAsync()).ConfigureAwait(false);
            return false;
        }

        static WindowGroup Migrate(LegacyWindowGroup legacyGroup) {
            var result = new WindowGroup {Name = legacyGroup.Name};
            foreach (var legacyWindowFilter in legacyGroup.Filters) {
                WindowFilter filter = Migrate(legacyWindowFilter);
                result.Filters.Add(filter);
            }
            return result;
        }

        static WindowFilter Migrate(Models.Legacy.Filters.WindowFilter legacyWindowFilter) =>
            new WindowFilter {
                ClassFilter = Migrate(legacyWindowFilter.ClassFilter),
                TitleFilter = Migrate(legacyWindowFilter.TitleFilter),
            };

        static CommonStringMatchFilter Migrate(Models.Legacy.Filters.CommonStringMatchFilter filter) =>
            filter == null
                ? null
                : new CommonStringMatchFilter {
                    Match = (CommonStringMatchFilter.MatchOption)(int)filter.Match,
                    Value = filter.Value,
                };
    }
}

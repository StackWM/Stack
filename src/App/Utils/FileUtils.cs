namespace LostTech.Stack.Utils {
    using System;
    using System.IO;
    using JetBrains.Annotations;

    class FileUtils {
        public static class IOResult
        {
            public const int FileAlreadyExists = -2147024816;
        }

        public static void CopyFiles([NotNull] DirectoryInfo from, [NotNull] DirectoryInfo to, bool overwrite = false) {
            if (@from == null) throw new ArgumentNullException(nameof(@from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (!from.Exists)
                return;
            if (!to.Exists)
                throw new DirectoryNotFoundException();

            foreach (var subdir in from.GetDirectories()) {
                var toSubdir = to.CreateSubdirectory(subdir.Name);
                CopyFiles(from: subdir, to: toSubdir);
            }

            foreach (FileInfo file in from.GetFiles())
                file.CopyTo(Path.Combine(to.FullName, file.Name), overwrite: overwrite);
        }

        public static void CopyFiles([NotNull] string from, [NotNull] string to, bool overwrite = false) {
            if (@from == null) throw new ArgumentNullException(nameof(@from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            var fromDirectory = new DirectoryInfo(from);
            var toDirectory = new DirectoryInfo(to);
            CopyFiles(from: fromDirectory, to: toDirectory, overwrite: overwrite);
        }
    }
}

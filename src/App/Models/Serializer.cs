namespace LostTech.Stack.Models
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using System.Xml;

    using PCLStorage;
    using LostTech.Stack.InternalExtensions;

    static class Serializer
    {
        public static async Task Save<T>(IFile file, T @object, DataContractSerializer serializer = null)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            serializer = serializer ?? new DataContractSerializer(typeof(T));

            using (var stream = await file.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false))
            using (var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true }))
            {
                serializer.WriteObject(xmlWriter, @object);
            }
        }

        public static async Task<T> Deserialize<T>(IFile file)
        {
            var serializer = new DataContractSerializer(typeof(T));

            using (var stream = await file.OpenAsync(FileAccess.Read).ConfigureAwait(false))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        public static async Task<T> Deserialize<T>(IFolder folder, string relativePath)
            where T: class
        {
            if (folder == null)
                throw new ArgumentNullException(nameof(folder));
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentNullException(nameof(relativePath));
            if (relativePath.Contains("/") || relativePath.Contains("\\"))
                throw new NotImplementedException();

            var file = await folder.GetFileOrNull(relativePath).ConfigureAwait(false);
            if (file == null)
                return null;
            return await Deserialize<T>(file).ConfigureAwait(false);
        }
    }
}

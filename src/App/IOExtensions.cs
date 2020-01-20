namespace LostTech.Stack.InternalExtensions
{
	using System;
    using System.IO;
    using System.Threading.Tasks;

	static class IoExtensions
	{
		public static async Task<string[]> ReadLinesAsync(this FileInfo file)
		{
			string text = await File.ReadAllTextAsync(file.Name).ConfigureAwait(false);
			return text.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
		}

		public static async Task<FileInfo> GetFileOrNull(this DirectoryInfo folder, string name)
		{
			try
			{
				var result = new FileInfo(Path.Combine(folder.FullName, name));
				return result.Exists ? result : null;
			}
			catch (FileNotFoundException)
			{
				return null;
			}
		}
	}
}

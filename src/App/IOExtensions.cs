namespace LostTech.Stack.InternalExtensions
{
	using System;
	using System.Threading.Tasks;
	using PCLStorage;

	static class IoExtensions
	{
		public static async Task<string[]> ReadLinesAsync(this IFile file)
		{
			var text = await file.ReadAllTextAsync().ConfigureAwait(false);
			return text.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
		}

		public static async Task<IFile> GetFileOrNull(this IFolder folder, string name)
		{
			try
			{
				return await folder.GetFileAsync(name).ConfigureAwait(false);
			}
			catch (PCLStorage.Exceptions.FileNotFoundException)
			{
				return null;
			}
			catch (System.IO.FileNotFoundException)
			{
				return null;
			}
		}
	}
}

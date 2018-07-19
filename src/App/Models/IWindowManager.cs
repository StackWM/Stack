namespace LostTech.Stack.Models {
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using LostTech.Stack.WindowManagement;

    interface IWindowManager
    {
        Task<bool?> Detach([NotNull] IAppWindow window, bool restoreBounds = false);
    }
}

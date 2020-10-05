#nullable enable
namespace LostTech.Stack.Models {
    using System.Threading.Tasks;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Zones;

    interface IWindowManager
    {
        Task<bool?> Detach(IAppWindow window, bool restoreBounds = false);
        Task Move(IAppWindow window, Zone target);
    }
}

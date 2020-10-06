#nullable enable
namespace LostTech.Stack.Utils {
    using System;

    static class IoCUtils {
        public static T Get<T>(this IServiceProvider serviceProvider) {
            if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));

            return (T)serviceProvider.GetService(typeof(T));
        }
    }
}

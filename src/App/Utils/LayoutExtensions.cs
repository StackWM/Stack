using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LostTech.Stack.Zones;

namespace LostTech.Stack.Utils;

static class LayoutExtensions {
    public static async Task<bool> AllReady(this IEnumerable<ScreenLayout> layouts,
                                            CancellationToken cancellation = default) {
        while (!cancellation.IsCancellationRequested) {
            var layoutElements = layouts.Select(l => l.Layout).ToArray();
            if (layoutElements.Contains(null)) {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellation);
            } else {
                await Task.WhenAll(layoutElements.Select(Layout.GetReady));
                return true;
            }
        }

        return false;
    }
}

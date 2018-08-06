namespace LostTech.Stack.Utils {
    using System;
    using System.Threading.Tasks;

    class Retry {
        public static T Times<T>(int attempts, Func<bool, T> func) {
            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts));

            for (int i = 0; i < attempts - 1; i++) {
                try {
                    return func(false);
                } catch (RetriableException) { }
            }

            return func(true);
        }

        public static async Task<T> TimesAsync<T>(int attempts, Func<bool, Task<T>> func, bool captureContext = true) {
            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts));

            for (int i = 0; i < attempts - 1; i++) {
                try {
                    return await func(false).ConfigureAwait(continueOnCapturedContext: captureContext);
                } catch (RetriableException) { }
            }

            return await func(true).ConfigureAwait(continueOnCapturedContext: captureContext);
        }

        public static async Task TimesAsync(int attempts, Func<bool, Task> func, bool captureContext = true) {
            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts));

            for (int i = 0; i < attempts - 1; i++) {
                try {
                    await func(false).ConfigureAwait(continueOnCapturedContext: captureContext);
                    return;
                } catch (RetriableException) { }
            }

            await func(true).ConfigureAwait(continueOnCapturedContext: captureContext);
        }
    }
}

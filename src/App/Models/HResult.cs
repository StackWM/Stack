// ReSharper disable InconsistentNaming
namespace LostTech.Stack.Models {
    public enum HResult:uint {
        TYPE_E_ELEMENTNOTFOUND = 0x8002802B,
    }

    public static class HResultExtensions
    {
        public static bool EqualsCode(this HResult result, int code) => result == (HResult)code;
    }
}

using Windows.Win32.Foundation;

// ReSharper disable once CheckNamespace
namespace Windows.Win32
{
    internal partial class PInvoke
    {
        internal static unsafe uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId)
        {
            fixed (uint* lpdwProcessIdPointer = &lpdwProcessId)
                return GetWindowThreadProcessId(hWnd, lpdwProcessIdPointer);
        }
    }
}
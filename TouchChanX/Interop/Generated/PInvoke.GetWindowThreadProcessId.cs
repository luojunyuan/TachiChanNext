// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------

#pragma warning disable CS1591,CS1573,CS0465,CS0649,CS8019,CS1570,CS1584,CS1658,CS0436,CS8981
using global::System;
using global::System.Diagnostics;
using global::System.Diagnostics.CodeAnalysis;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using winmdroot = global::Windows.Win32;
namespace Windows.Win32
{

	/// <content>
	/// Contains extern methods from "USER32.dll".
	/// </content>
	internal static partial class PInvoke
	{
		/// <summary>Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the process that created the window.</summary>
		/// <param name="hWnd">
		/// <para>Type: <b>HWND</b> A handle to the window.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <param name="lpdwProcessId">
		/// <para>Type: <b>LPDWORD</b> A pointer to a variable that receives the process identifier. If this parameter is not <b>NULL</b>, <b>GetWindowThreadProcessId</b> copies the identifier of the process to the variable; otherwise, it does not. If the function fails, the value of the variable is unchanged.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <returns>
		/// <para>Type: <b>DWORD</b> If the function succeeds, the return value is the identifier of the thread that created the window. If the window handle is invalid, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
		/// </returns>
		/// <remarks>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid">Learn more about this API from docs.microsoft.com</see>.</para>
		/// </remarks>
		[DllImport("USER32.dll", ExactSpelling = true),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		internal static extern unsafe uint GetWindowThreadProcessId(winmdroot.Foundation.HWND hWnd, [Optional] uint* lpdwProcessId);
	}
}

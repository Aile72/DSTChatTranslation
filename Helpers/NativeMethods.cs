using System.Runtime.InteropServices;

namespace DSTChatTranslation.Helpers;

public static class NativeMethods
{
	#region  Windows API Constants
	public static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
	{
		return Environment.Is64BitProcess
			? GetWindowLong64(hWnd, nIndex)
			: GetWindowLong32(hWnd, nIndex);
	}

	public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
	{
		return Environment.Is64BitProcess
			? SetWindowLong64(hWnd, nIndex, dwNewLong)
			: SetWindowLong32(hWnd, nIndex, dwNewLong);
	}

	[DllImport("user32.dll", EntryPoint = "GetWindowLong")]
	private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
	private static extern IntPtr GetWindowLong64(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
	#endregion
}
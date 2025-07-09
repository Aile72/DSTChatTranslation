using System.Diagnostics;
using System.Windows.Interop;
using DSTChatTranslation.Helpers;
using DSTChatTranslation.Views;

namespace DSTChatTranslation.Services;

public class WindowStateService(MainWindow mainWindow)
{
	public const int WS_EX_NOACTIVATE = 0x08000000;	// 不获取焦点
	public const int WS_EX_TRANSPARENT = 0x20;		// 鼠标穿透
	public readonly int GWL_EXSTYLE = -20;          // 扩展窗口风格索引

	/// <summary>
	/// 应用窗口锁定状态
	/// </summary>
	public void ApplyWindowLockState(bool isLocked)
	{
		Debug.WriteLine($"应用窗口锁定状态: {isLocked}");
		var handle = new WindowInteropHelper(mainWindow).Handle;
		var exstyle = NativeMethods.GetWindowLong(handle, GWL_EXSTYLE);

		if (isLocked)
		{
			// 锁定：设置穿透且不激活
			NativeMethods.SetWindowLong(handle, GWL_EXSTYLE,
				new IntPtr(exstyle.ToInt32() | WS_EX_TRANSPARENT |WS_EX_NOACTIVATE));
		}
		else
		{
			// 解锁：移除穿透，恢复正常窗口
			NativeMethods.SetWindowLong(handle, GWL_EXSTYLE,
				new IntPtr(exstyle.ToInt32() & ~WS_EX_TRANSPARENT & ~WS_EX_NOACTIVATE));
		}
	}
}
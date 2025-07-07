using System.Diagnostics;

namespace DSTChatTranslation.Helpers;

public static class OpenUrlHandler
{
	/// <summary>
	/// 尝试用默认浏览器打开网址
	/// </summary>
	public static void OpenUrlInDefaultBrowser(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		catch (Exception)
		{
		}
	}
}

using System.Diagnostics;
using System.Reflection;

namespace DSTChatTranslation.Helpers;

public class GetCurrentVersion
{
	/// <summary>
	/// 获取当前程序版本号
	/// </summary>
	public static string GetVersion()
	{
		try
		{
			// 获取入口程序集
			var assembly = Assembly.GetEntryAssembly();

			// 优先获取信息版本
			var infoVersion = assembly?
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
				.InformationalVersion;

			if (!string.IsNullOrWhiteSpace(infoVersion))
			{
				Debug.WriteLine($"当前信息版本: {infoVersion}");
				return infoVersion;
			}

			// 如果信息版本不存在，获取程序集版本
			var version = assembly?.GetName().Version;
			Debug.WriteLine($"当前程序集版本: {version}");
			return version?.ToString() ?? "1.0.0";
		}
		catch
		{
			// 获取失败时返回默认版本
			Debug.WriteLine("获取版本失败! 返回默认版本");
			return "1.0.0";
		}
	}
}

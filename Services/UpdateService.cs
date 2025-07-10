using System.Diagnostics;
using Application = System.Windows.Application;
using DSTChatTranslation.Models;
using DSTChatTranslation.Views;

namespace DSTChatTranslation.Services;

public class UpdateService(
	MainWindow mainWindow,
	FileMonitorService fileMonitorService,
	MessageService messageService)
{
	private readonly object _queueLock = new();

	/// <summary>
	/// 读取文件并更新UI
	/// </summary>
	public async void ReadFileAndUpdateUI()
	{
		if (!Application.Current.Dispatcher.CheckAccess() ||
		!mainWindow.IsLoaded ||
		mainWindow.IsClosed) // 需要实现IsClosed属性
		{
			return;
		}

		CancellationToken token;
		lock (_queueLock)
		{
			// 检查取消令牌
			if (MainWindow.cancellationTokenSource == null ||
				MainWindow.cancellationTokenSource.IsCancellationRequested)
			{
				return;
			}
			token = MainWindow.cancellationTokenSource.Token;
		}

		try
		{
			var newLines = await fileMonitorService.GetNewChatLinesAsync(token)
				.ConfigureAwait(false);

			// 确保在UI线程更新显示
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				// 更新字体大小
				UpdateFontSize();

				// 处理新消息
				if (newLines.Count > 0)
				{
					foreach (var (line, id, nickName, chat) in newLines)
					{
						messageService.AddMessage(new ChatLineNameChatPair
						{
							Line = $"{line}|{DateTime.Now.Ticks}",
							Id = id,
							Name = nickName,
							Chat = chat
						});
					}
				}
			}, System.Windows.Threading.DispatcherPriority.Normal, token);
		}
		catch (OperationCanceledException)
		{
			// 正常取消操作，无需处理
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"ReadFileAndUpdateUI 异常: {ex.Message}");
			messageService.AddSystemMessage($"Application error: {ex.Message}");
		}
	}

	/// <summary>
	/// 更新字体大小
	/// </summary>
	private void UpdateFontSize()
	{
		try
		{
			double baseSize = 12;
			double scaleFactor = (AppSettingsModel.Current.FontSizeMultiple + 10) * 0.1;
			double newFontSize = Math.Max(6, baseSize * scaleFactor);

			mainWindow.outputTextBlock.FontSize = newFontSize;
		}
		catch
		{
			mainWindow.outputTextBlock.FontSize = 12;
		}
	}
}
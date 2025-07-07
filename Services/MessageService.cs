using System.Windows.Documents;
using Brushes = System.Windows.Media.Brushes;
using Application = System.Windows.Application;
using DSTChatTranslation.Models;
using DSTChatTranslation.Views;

namespace DSTChatTranslation.Services;

public class MessageService(MainWindow mainWindow)
{
	public Queue<ChatLineNameChatPair> messageQueue = new();
	private const int MaxMessages = 10;

	private readonly object _lock = new();

	/// <summary>
	/// 添加一条消息到消息队列
	/// </summary>
	/// <param name="message"></param>
	public void AddMessage(ChatLineNameChatPair message)
	{
		lock (_lock)
		{
			// 添加新消息
			messageQueue.Enqueue(message);

			// 保持队列不超过最大数量
			while (messageQueue.Count > MaxMessages)
			{
				messageQueue.Dequeue();
			}
		}

		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			UpdateMessageDisplay();
		});
	}

	/// <summary>
	/// 添加一条系统消息到消息队列
	/// </summary>
	/// <param name="message"></param>
	public void AddSystemMessage(string message)
	{
		AddMessage(new ChatLineNameChatPair
		{
			Line = $"[System]|{DateTime.Now.Ticks}",
			Name = "[System]",
			Chat = message
		});
	}

	/// <summary>
	/// 更新消息显示
	/// </summary>
	public void UpdateMessageDisplay()
	{
		lock (_lock)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				// 清除现有内容
				mainWindow.outputTextBlock.Inlines.Clear();

				// 添加所有消息
				foreach (var pair in messageQueue)
				{
					Run nickNameRun = new()
					{
						Text = pair.Name,
						Foreground = pair.Name == "[System]"
							? Brushes.Yellow : Brushes.Green
					};

					Run chatRun = new()
					{
						Text = $": {pair.Chat}",
						Foreground = Brushes.White
					};

					mainWindow.outputTextBlock.Inlines.Add(nickNameRun);
					mainWindow.outputTextBlock.Inlines.Add(chatRun);
					mainWindow.outputTextBlock.Inlines.Add(new LineBreak());
				}
			});
		}
	}
}
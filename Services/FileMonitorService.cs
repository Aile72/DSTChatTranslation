using System.IO;
using System.Text;
using System.Diagnostics;
using DSTChatTranslation.Helpers;
using DSTChatTranslation.Views;

namespace DSTChatTranslation.Services
{
	public class FileMonitorService : IDisposable
	{
		private readonly string _filePath;
		private long _lastKnownSize;
		private string _remainingContent = string.Empty;
		private readonly object _fileLock = new();
		private readonly MessageService _messageService;
		private readonly System.Threading.Timer _pollingTimer;

		public FileMonitorService(string filePath, MessageService messageService)
		{
			_filePath = filePath;
			_messageService = messageService;

			// 初始化文件
			InitializeFile();

			_pollingTimer = new System.Threading.Timer(CheckFileChanges, null, 0, 100);
		}

		/// <summary>
		/// 定时检查文件变化并更新UI
		/// </summary>
		/// <param name="state"></param>
		private static void CheckFileChanges(object? state)
		{
			try
			{
				MainWindow.Instance?.updateService?.ReadFileAndUpdateUI();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"定时文件检查错误: {ex.Message}");
			}
		}
		/// <summary>
		/// 初始化文件，如果文件不存在则创建它
		/// </summary>

		private void InitializeFile()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					File.Create(_filePath).Close();
				}
				_lastKnownSize = GetFileSizeSafe();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"文件初始化失败: {ex.Message}");
			}
		}

		/// <summary>
		/// 异步获取新的聊天行
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public async Task<List<(string line, string id, string nickName, string chat)>> GetNewChatLinesAsync(CancellationToken token)
		{
			var result = new List<(string line, string id, string nickName, string chat)>();
			long currentSize;
			try
			{
				// 获取当前文件大小
				currentSize = GetFileSizeSafe();

				// 检查文件是否被截断
				if (currentSize < _lastKnownSize)
				{
					_lastKnownSize = 0;
					_remainingContent = string.Empty;
					currentSize = GetFileSizeSafe();
				}

				// 没有新内容或已取消
				if (currentSize <= _lastKnownSize || token.IsCancellationRequested)
				{
					return result;
				}

				// 读取新内容
				using var fs = new FileStream(
					_filePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.ReadWrite | FileShare.Delete,
					4096,
					FileOptions.SequentialScan);
				fs.Seek(_lastKnownSize, SeekOrigin.Begin);
				byte[] buffer = new byte[currentSize - _lastKnownSize];
				int bytesRead = await fs.ReadAsync(buffer, token);

				if (bytesRead == 0)
					return result;

				// 处理内容
				string newContent = Encoding.UTF8.GetString(buffer, 0, bytesRead);
				string fullContent = _remainingContent + newContent;
				_remainingContent = string.Empty;

				string[] lines = fullContent.Split('\n');
				int completeLineCount = lines.Length;

				// 处理最后一行不完整的情况
				if (!fullContent.EndsWith('\n') && lines.Length > 0)
				{
					_remainingContent = lines[^1];
					completeLineCount = lines.Length - 1;
				}

				_lastKnownSize = currentSize;

				// 处理完整行
				for (int i = 0; i < completeLineCount; i++)
				{
					string line = lines[i].TrimEnd('\r');
					if (line.Length < 18) continue;

					string subString = line.Substring(11, Math.Min(7, line.Length - 11));
					if (!subString.Contains("[Say]")) continue;

					string talkLine = line.Length > 17 ? line[17..] : "";
					int colonIndex = talkLine.IndexOf(':');
					if (colonIndex == -1) continue;

					var (id, nickName) = RegexHelper.ProcessIdAndName(line);

					string originalChat = colonIndex + 1 < talkLine.Length
						? talkLine[(colonIndex + 1)..].Trim()
						: "";

					result.Add((line, id, nickName, originalChat));
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"读取文件失败: {ex.Message}");
				_messageService.AddSystemMessage($"File read error: {ex.GetType().Name}");
				return result;
			}

			// 并行翻译所有消息
			var translationTasks = new List<Task<(string line, string id, string nickName, string chat)>>();
			foreach (var item in result)
			{
				if (MainWindow.Instance != null)
				{
					translationTasks.Add(MainWindow.Instance.translationService.TranslateMessageAsync(item, token));
				}
			}

			var translatedResults = await Task.WhenAll(translationTasks);
			return [.. translatedResults];
		}

		/// <summary>
		/// 获取文件大小，失败时返回上次已知大小
		/// </summary>
		/// <returns></returns>
		private long GetFileSizeSafe()
		{
			try
			{
				return new FileInfo(_filePath).Length;
			}
			catch
			{
				// 失败时返回上次已知大小
				return _lastKnownSize;
			}
		}

		/// <summary>
		/// 释放资源
		/// </summary>
		public void Dispose()
		{
			_pollingTimer?.Dispose(); // 释放定时器
			GC.SuppressFinalize(this);
		}
	}
}
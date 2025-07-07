namespace DSTChatTranslation.Helpers;

public class LRUCache<TKey, TValue> where TKey : notnull
{
	private readonly object _lock = new();
	private readonly int _capacity;
	private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap = [];
	private readonly LinkedList<CacheItem> _lruList = new();

	/// <summary>
	/// LRUCache 构造函数
	/// </summary>
	/// <param name="capacity"></param>
	/// <exception cref="ArgumentException"></exception>
	public LRUCache(int capacity)
	{
		if (capacity <= 0) throw new ArgumentException("Capacity must be positive", nameof(capacity));
		_capacity = capacity;
	}

	/// <summary>
	/// 尝试获取缓存中的值，如果存在则返回true并输出值，否则返回false
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public bool TryGet(TKey key, out TValue value)
	{
		lock (_lock)
		{
			if (_cacheMap.TryGetValue(key, out var node))
			{
				value = node.Value.Value;
				_lruList.Remove(node);
				_lruList.AddFirst(node);
				return true;
			}
			value = default!;
			return false;
		}
	}

	/// <summary>
	/// 添加一个键值对到缓存中，如果缓存已满则移除最少使用的项
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	public void Add(TKey key, TValue value)
	{
		lock (_lock)
		{
			if (_cacheMap.Count >= _capacity)
			{
				var last = _lruList.Last!;
				_cacheMap.Remove(last.Value.Key);
				_lruList.RemoveLast();
			}

			var cacheItem = new CacheItem(key, value);
			var node = new LinkedListNode<CacheItem>(cacheItem);
			_lruList.AddFirst(node);
			_cacheMap[key] = node;
		}
	}

	/// <summary>
	/// 清空缓存
	/// </summary>
	public void Clear()
	{
		_cacheMap.Clear();
		_lruList.Clear();
	}

	/// <summary>
	/// 获取当前缓存的大小
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	private class CacheItem(TKey key, TValue value)
	{
		public TKey Key { get; } = key;
		public TValue Value { get; } = value;
	}
}
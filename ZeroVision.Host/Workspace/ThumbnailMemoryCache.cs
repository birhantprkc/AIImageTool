using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace ZeroVision.Host.Workspace;

/// <summary>
/// Tier 1 In-Memory LRU Cache for decoded WPF thumbnails.
/// Drastically eliminates disk seek &amp; JPEG decoding churn during rapid filmstrip &amp; grid scrolling.
/// </summary>
public static class ThumbnailMemoryCache
{
    private const int MaxCapacity = 1000;
    private static readonly Dictionary<string, LinkedListNode<CacheEntry>> _index = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<CacheEntry> _lru = new();
    private static readonly object _sync = new();

    private record CacheEntry(string Path, BitmapSource Bitmap);

    public static BitmapSource? Get(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        lock (_sync)
        {
            if (_index.TryGetValue(path, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Bitmap;
            }
            return null;
        }
    }

    public static BitmapSource? GetOrLoad(string path, int decodeWidth = 256)
    {
        if (string.IsNullOrEmpty(path)) return null;

        lock (_sync)
        {
            if (_index.TryGetValue(path, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
            bi.EndInit();
            bi.Freeze();

            lock (_sync)
            {
                if (_index.TryGetValue(path, out var existing))
                {
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                    return existing.Value.Bitmap;
                }

                if (_index.Count >= MaxCapacity && _lru.Last != null)
                {
                    _index.Remove(_lru.Last.Value.Path);
                    _lru.RemoveLast();
                }

                var entry = new CacheEntry(path, bi);
                var newNode = _lru.AddFirst(entry);
                _index[path] = newNode;
            }

            return bi;
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        lock (_sync)
        {
            _index.Clear();
            _lru.Clear();
        }
    }
}

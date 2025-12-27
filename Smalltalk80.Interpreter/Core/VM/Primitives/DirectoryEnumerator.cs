namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Manages directory enumeration state for FindFirst/FindNext primitives.
/// </summary>
internal static class DirectoryEnumerator
{
    private static readonly Dictionary<int, IEnumerator<string>> _enumerators = [];
    private static int _nextHandle = 1;
    private static readonly object _lock = new();

    public static int FindFirst(string pattern)
    {
        lock (_lock)
        {
            try
            {
                string? directory = Path.GetDirectoryName(pattern);
                string searchPattern = Path.GetFileName(pattern);

                if (string.IsNullOrEmpty(directory)) directory = ".";
                if (string.IsNullOrEmpty(searchPattern)) searchPattern = "*";

                var entries = Directory.EnumerateFileSystemEntries(directory, searchPattern);
                var enumerator = entries.GetEnumerator();

                int handle = _nextHandle++;
                _enumerators[handle] = enumerator;
                return handle;
            }
            catch
            {
                return -1;
            }
        }
    }

    public static string? FindNext(int handle)
    {
        lock (_lock)
        {
            if (_enumerators.TryGetValue(handle, out var enumerator))
            {
                if (enumerator.MoveNext())
                {
                    return Path.GetFileName(enumerator.Current);
                }
            }
            return null;
        }
    }

    public static bool FindClose(int handle)
    {
        lock (_lock)
        {
            if (_enumerators.TryGetValue(handle, out var enumerator))
            {
                enumerator.Dispose();
                _enumerators.Remove(handle);
                return true;
            }
            return false;
        }
    }
}

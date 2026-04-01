using System.Collections;

namespace Minnaloushe.Core.Toolbox.Collections;

public class ConcurrentCircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;
    private readonly Lock _lock = new();

    public ConcurrentCircularBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        _buffer = new T[capacity];
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % Capacity;

            if (_count < Capacity)
            {
                _count++;
            }
        }
    }

    public T[] ToArray()
    {
        lock (_lock)
        {
            var result = new T[_count];
            for (var i = 0; i < _count; i++)
            {
                var idx = InnerIndex(i);
                result[i] = _buffer[idx];
            }
            return result;
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        lock (_lock)
        {
            if (array.Length - arrayIndex < _count)
            {
                throw new ArgumentException("Destination array is not large enough to copy the elements.", nameof(array));
            }

            for (var i = 0; i < _count; i++)
            {
                var idx = InnerIndex(i);
                array[arrayIndex + i] = _buffer[idx];
            }
        }
    }

    private int InnerIndex(int outerIndex) => (_head - _count + outerIndex + Capacity) % Capacity;

    public void Clear()
    {
        lock (_lock)
        {
            _count = 0;
            _head = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
        {
            // Snapshot to avoid modification during enumeration
            return ToArray().AsEnumerable().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}
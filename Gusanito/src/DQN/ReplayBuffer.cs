namespace Gusanito.DQN;

/// <summary>
/// Circular buffer thread-safe para experience replay.
/// Push desde el training thread, Sample también desde el training thread.
/// El lock es ligero — no hay contención real en uso single-threaded.
/// </summary>
public sealed class ReplayBuffer
{
    private readonly Experience[] _buffer;
    private readonly int          _capacity;
    private int                   _head;
    private int                   _count;
    private readonly object       _lock = new();

    public int Count { get { lock (_lock) return _count; } }
    public bool IsReady(int minSamples) => Count >= minSamples;

    public ReplayBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer   = new Experience[capacity];
        _capacity = capacity;
    }

    public void Push(in Experience exp)
    {
        lock (_lock)
        {
            _buffer[_head] = exp;
            _head          = (_head + 1) % _capacity;
            _count         = Math.Min(_count + 1, _capacity);
        }
    }

    /// <summary>
    /// Fisher-Yates parcial — O(batchSize) en vez de O(capacity).
    /// Nunca aloca el array completo para hacer shuffle.
    /// </summary>
    public Experience[] Sample(int batchSize, Random rng)
    {
        lock (_lock)
        {
            if (batchSize > _count)
                throw new InvalidOperationException(
                    $"Cannot sample {batchSize} from buffer with {_count} entries.");

            var indices = new int[_count];
            for (int i = 0; i < _count; i++) indices[i] = i;

            // Partial Fisher-Yates
            for (int i = 0; i < batchSize; i++)
            {
                int j    = i + rng.Next(_count - i);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var batch = new Experience[batchSize];
            for (int i = 0; i < batchSize; i++)
                batch[i] = _buffer[indices[i]];

            return batch;
        }
    }
}
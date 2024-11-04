namespace RorCs;

/// Ring buffer implementation with support for multiple readers.
/// Can be used in a single threaded environment or
/// with one writing thread and one or multiple reading threads.
public class RingBuffer<T>
{
    private readonly T[] ring;
    private int cursor = 0; // Write position
    private readonly int[] gate; // Read positions per reader
    public int Capacity { get; }
    public int Cursor => cursor;
    private readonly ManualResetEventSlim dataWrittenEvent;

    public RingBuffer(int capacity, int readerCount, ManualResetEventSlim dataWrittenEvent)
    {
        Capacity = capacity;
        ring = new T[capacity];
        gate = new int[readerCount];
        this.dataWrittenEvent = dataWrittenEvent;
    }

    /// Function to check if the ring buffer is empty for a specific reader
    public bool IsEmpty(int readerId)
    {
        return cursor == gate[readerId];
    }

    /// Function to check if the ring buffer is full
    public bool IsFull
    {
        get
        {
            int minValue = gate[0];
            for (int i = 1; i < gate.Length; i++)
            {
                if (gate[i] < minValue)
                {
                    minValue = gate[i];
                }
            }
            // Need to add 2 to avoid deadlocks when, with multiple 
            // consumers one of them is full and some other is empty.
            return (cursor + 2) % Capacity == minValue;
        }
    }

    /// Function to write a value to the ring buffer.
    /// Can fail and in this case will return false.
    public bool Write(T value)
    {
        if (IsFull)
        {
            return false;
        }
        else
        {
            ring[cursor] = value;
            cursor = (cursor + 1) % Capacity;
            dataWrittenEvent?.Set(); // Signal that new data has been written
            return true;
        }
    }

    /// Function to read a value from the ring buffer for a specific reader.
    /// If the ring is empty for that reader, will return option None.
    public bool Read(int readerId, out T value)
    {
        if (IsEmpty(readerId))
        {
            value = default;
            return false;
        }
        else
        {
            int gatePosition = gate[readerId];
            value = ring[gatePosition];
            gate[readerId] = (gatePosition + 1) % Capacity;
            return true;
        }
    }

    /// Function to write a value and spin if the ring is full.
    public void SpinWrite(T value)
    {
        while (IsFull)
        {
            Thread.SpinWait(1);
        }
        ring[cursor] = value;
        cursor = (cursor + 1) % Capacity;
        dataWrittenEvent?.Set(); // Signal that new data has been written
    }

    /// Function to read a value from the ring buffer for a specific reader, spinning if empty.
    public T SpinRead(int readerId)
    {
        while (IsEmpty(readerId))
        {
            Thread.SpinWait(1);
        }
        int gatePosition = gate[readerId];
        T value = ring[gatePosition];
        gate[readerId] = (gatePosition + 1) % Capacity;
        return value;
    }

    // Function to reset the buffer (for testing only).
    public void ResetBuffer()
    {
        for (int i = 0; i < Capacity; i++)
        {
            ring[i] = default;
        }
        cursor = 0;
        for (int i = 0; i < gate.Length; i++)
        {
            gate[i] = 0;
        }
    }
}
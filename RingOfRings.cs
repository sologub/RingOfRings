namespace RorCs;

/// <summary>
/// This is a simple Ring Of Rings implementation.
/// We have multiple producers, one single ring buffer, and multiple consumers.
/// Producers and consumers are organized as two collections of ring buffers.
/// A stale consumer will block the entire Ring Of Rings system.
/// </summary>
/// <typeparam name="T">The type of elements.</typeparam>
public class RingOfRings<T>
{
    private const int Capacity = 1024; // ring buffers capacity, must be power of 2 for best performance.
    private RingBuffer<T>[] producers = [];
    private readonly RingBuffer<T> ring;
    private RingBuffer<T>[] consumers = [];

    private readonly ManualResetEventSlim dataProducedEvent = new(false);
    private readonly ManualResetEventSlim dataImportedEvent = new(false);
    bool running;

    public RingOfRings()
    {
        ring = new RingBuffer<T>(Capacity, 1, dataImportedEvent);
    }

    public RingBuffer<T> AddProducer()
    {
        var producer = new RingBuffer<T>(Capacity, 1, dataProducedEvent);
        producers = producers.Concat([producer]).ToArray();
        return producer;
    }

    public void RemoveProducer(RingBuffer<T> producer)
    {
        producers = producers.Except([producer]).ToArray();
    }

    public RingBuffer<T> AddConsumer()
    {
        var consumer = new RingBuffer<T>(Capacity, 1, null);
        consumers = consumers.Concat([consumer]).ToArray();
        return consumer;
    }

    public void RemoveConsumer(RingBuffer<T> consumer)
    {
        consumers = consumers.Except([consumer]).ToArray();
    }

    public void Start(CancellationToken token)
    {
        running = true;

        // Import data from producers into the main ring.
        void Import()
        {
            while (running && !token.IsCancellationRequested)
            {
                dataProducedEvent.Wait(token); // Wait for data to be produced

                foreach (var producer in producers)
                {
                    while (producer.Read(0, out var @event))
                    {

                        ring.SpinWrite(@event); // write to main ring
                    }
                }
            }
        }

        // Export data from the main ring to the consumers.
        void Export()
        {
            while (running && !token.IsCancellationRequested)
            {
                // Use an AutoResetEvent to lower CPU utilization.
                dataImportedEvent.Wait(token); // Wait for data to be imported

                while (ring.Read(0, out var ev))
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.SpinWrite(ev);
                    }
                }
            }
        }

        var importThread = new Thread(Import) { IsBackground = true };
        var exportThread = new Thread(Export) { IsBackground = true };

        importThread.Start();
        exportThread.Start();
    }

    public void Stop()
    {
        running = false;
    }
}
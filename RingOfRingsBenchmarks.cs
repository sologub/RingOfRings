namespace RorCs.Benchmarks;

using System.Diagnostics;

/// <summary>
/// Comprehensive benchmarks for RingOfRings implementation.
/// Measures throughput, latency, and scaling characteristics.
/// </summary>
public static class RingOfRingsBenchmarks
{
    private const int WarmupIterations = 100_000;

    public static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          RingOfRings Performance Benchmarks                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Warmup JIT
        Console.WriteLine("Warming up JIT...");
        WarmupJit();
        Console.WriteLine("Warmup complete.\n");

        // RingBuffer benchmarks
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                    RINGBUFFER BENCHMARKS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        Benchmark_RingBuffer_SingleThread_Throughput();
        Benchmark_RingBuffer_ProducerConsumer_Throughput();
        Benchmark_RingBuffer_Latency();

        // RingOfRings benchmarks
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                   RINGOFRING BENCHMARKS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        Benchmark_RingOfRings_1P1C_Throughput();
        Benchmark_RingOfRings_1P_MultiC_Throughput();
        Benchmark_RingOfRings_MultiP_1C_Throughput();
        Benchmark_RingOfRings_MultiP_MultiC_Throughput();
        Benchmark_RingOfRings_Latency();

        // Scaling benchmarks
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                    SCALING BENCHMARKS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        Benchmark_Scaling_Producers();
        Benchmark_Scaling_Consumers();
        Benchmark_Scaling_MessageCount();

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                    BENCHMARKS COMPLETE");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }

    private static void WarmupJit()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);
        for (int i = 0; i < WarmupIterations; i++)
        {
            buffer.SpinWrite(i);
            buffer.SpinRead(0);
        }

        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();
        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var writerThread = new Thread(() =>
        {
            for (int i = 0; i < WarmupIterations; i++)
                producer.SpinWrite(i);
        });
        var readerThread = new Thread(() =>
        {
            for (int i = 0; i < WarmupIterations; i++)
                consumer.SpinRead(0);
        });

        writerThread.Start();
        readerThread.Start();
        writerThread.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();
    }

    private static void PrintResult(string name, long messageCount, TimeSpan elapsed)
    {
        double throughput = messageCount / elapsed.TotalSeconds;
        double nsPerOp = elapsed.TotalNanoseconds / messageCount;
        Console.WriteLine($"  {name,-45} {throughput,12:N0} msg/sec  ({nsPerOp,6:F1} ns/op)");
    }

    private static void PrintLatencyResult(string name, double minNs, double maxNs, double avgNs, double p50Ns, double p99Ns)
    {
        Console.WriteLine($"  {name}");
        Console.WriteLine($"    Min: {minNs,10:F0} ns    Max: {maxNs,10:F0} ns    Avg: {avgNs,10:F0} ns");
        Console.WriteLine($"    P50: {p50Ns,10:F0} ns    P99: {p99Ns,10:F0} ns");
    }

    // ============================================================================
    // RINGBUFFER BENCHMARKS
    // ============================================================================

    private static void Benchmark_RingBuffer_SingleThread_Throughput()
    {
        const int count = 10_000_000;
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<long>(1024, 1, signal);

        // Benchmark Write + Read (same thread)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            buffer.SpinWrite(i);
            buffer.SpinRead(0);
        }
        sw.Stop();

        PrintResult("RingBuffer Single-Thread Write+Read", count, sw.Elapsed);
    }

    private static void Benchmark_RingBuffer_ProducerConsumer_Throughput()
    {
        const int count = 10_000_000;
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<long>(1024, 1, signal);

        var sw = Stopwatch.StartNew();

        var writer = new Thread(() =>
        {
            for (int i = 0; i < count; i++)
                buffer.SpinWrite(i);
        });

        var reader = new Thread(() =>
        {
            for (int i = 0; i < count; i++)
                buffer.SpinRead(0);
        });

        writer.Start();
        reader.Start();
        writer.Join();
        reader.Join();

        sw.Stop();
        PrintResult("RingBuffer Producer-Consumer (2 threads)", count, sw.Elapsed);
    }

    private static void Benchmark_RingBuffer_Latency()
    {
        const int count = 100_000;
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<long>(1024, 1, signal);
        var latencies = new long[count];
        var ready = new ManualResetEventSlim(false);

        var writer = new Thread(() =>
        {
            ready.Wait();
            for (int i = 0; i < count; i++)
            {
                buffer.SpinWrite(Stopwatch.GetTimestamp());
                Thread.SpinWait(10); // Small delay between writes
            }
        });

        var reader = new Thread(() =>
        {
            ready.Wait();
            for (int i = 0; i < count; i++)
            {
                long sentTime = buffer.SpinRead(0);
                long recvTime = Stopwatch.GetTimestamp();
                latencies[i] = recvTime - sentTime;
            }
        });

        writer.Start();
        reader.Start();
        ready.Set();
        writer.Join();
        reader.Join();

        // Convert to nanoseconds and calculate statistics
        double ticksPerNs = Stopwatch.Frequency / 1_000_000_000.0;
        var latenciesNs = latencies.Select(t => t / ticksPerNs).OrderBy(x => x).ToArray();

        PrintLatencyResult("RingBuffer Latency",
            latenciesNs.Min(),
            latenciesNs.Max(),
            latenciesNs.Average(),
            latenciesNs[(int)(count * 0.50)],
            latenciesNs[(int)(count * 0.99)]);
    }

    // ============================================================================
    // RINGOFRING BENCHMARKS
    // ============================================================================

    private static void Benchmark_RingOfRings_1P1C_Throughput()
    {
        const int count = 5_000_000;
        var ror = new RingOfRings<long>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var sw = Stopwatch.StartNew();

        var writer = new Thread(() =>
        {
            for (int i = 0; i < count; i++)
                producer.SpinWrite(i);
        });

        var reader = new Thread(() =>
        {
            for (int i = 0; i < count; i++)
                consumer.SpinRead(0);
        });

        writer.Start();
        reader.Start();
        writer.Join();
        reader.Join();

        sw.Stop();
        ror.Stop();
        cts.Cancel();

        PrintResult("RingOfRings 1P-1C", count, sw.Elapsed);
    }

    private static void Benchmark_RingOfRings_1P_MultiC_Throughput()
    {
        const int count = 2_000_000;
        const int numConsumers = 4;

        var ror = new RingOfRings<long>();
        var producer = ror.AddProducer();
        var consumers = new RingBuffer<long>[numConsumers];
        for (int i = 0; i < numConsumers; i++)
            consumers[i] = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var sw = Stopwatch.StartNew();

        var writer = new Thread(() =>
        {
            for (int i = 0; i < count; i++)
                producer.SpinWrite(i);
        });

        var readers = new Thread[numConsumers];
        for (int c = 0; c < numConsumers; c++)
        {
            int consumerId = c;
            readers[c] = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                    consumers[consumerId].SpinRead(0);
            });
        }

        writer.Start();
        foreach (var r in readers) r.Start();
        writer.Join();
        foreach (var r in readers) r.Join();

        sw.Stop();
        ror.Stop();
        cts.Cancel();

        PrintResult($"RingOfRings 1P-{numConsumers}C", count, sw.Elapsed);
    }

    private static void Benchmark_RingOfRings_MultiP_1C_Throughput()
    {
        const int countPerProducer = 500_000;
        const int numProducers = 4;

        var ror = new RingOfRings<long>();
        var producers = new RingBuffer<long>[numProducers];
        for (int i = 0; i < numProducers; i++)
            producers[i] = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var sw = Stopwatch.StartNew();

        var writers = new Thread[numProducers];
        for (int p = 0; p < numProducers; p++)
        {
            int producerId = p;
            writers[p] = new Thread(() =>
            {
                for (int i = 0; i < countPerProducer; i++)
                    producers[producerId].SpinWrite(i);
            });
        }

        var reader = new Thread(() =>
        {
            int total = countPerProducer * numProducers;
            for (int i = 0; i < total; i++)
                consumer.SpinRead(0);
        });

        foreach (var w in writers) w.Start();
        reader.Start();
        foreach (var w in writers) w.Join();
        reader.Join();

        sw.Stop();
        ror.Stop();
        cts.Cancel();

        PrintResult($"RingOfRings {numProducers}P-1C", countPerProducer * numProducers, sw.Elapsed);
    }

    private static void Benchmark_RingOfRings_MultiP_MultiC_Throughput()
    {
        const int countPerProducer = 500_000;
        const int numProducers = 4;
        const int numConsumers = 4;

        var ror = new RingOfRings<long>();
        var producers = new RingBuffer<long>[numProducers];
        for (int i = 0; i < numProducers; i++)
            producers[i] = ror.AddProducer();
        var consumers = new RingBuffer<long>[numConsumers];
        for (int i = 0; i < numConsumers; i++)
            consumers[i] = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var sw = Stopwatch.StartNew();

        var writers = new Thread[numProducers];
        for (int p = 0; p < numProducers; p++)
        {
            int producerId = p;
            writers[p] = new Thread(() =>
            {
                for (int i = 0; i < countPerProducer; i++)
                    producers[producerId].SpinWrite(i);
            });
        }

        var readers = new Thread[numConsumers];
        int totalMessages = countPerProducer * numProducers;
        for (int c = 0; c < numConsumers; c++)
        {
            int consumerId = c;
            readers[c] = new Thread(() =>
            {
                for (int i = 0; i < totalMessages; i++)
                    consumers[consumerId].SpinRead(0);
            });
        }

        foreach (var w in writers) w.Start();
        foreach (var r in readers) r.Start();
        foreach (var w in writers) w.Join();
        foreach (var r in readers) r.Join();

        sw.Stop();
        ror.Stop();
        cts.Cancel();

        PrintResult($"RingOfRings {numProducers}P-{numConsumers}C", totalMessages, sw.Elapsed);
    }

    private static void Benchmark_RingOfRings_Latency()
    {
        const int count = 50_000;
        var ror = new RingOfRings<long>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();
        var latencies = new long[count];
        var ready = new ManualResetEventSlim(false);

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        var writer = new Thread(() =>
        {
            ready.Wait();
            for (int i = 0; i < count; i++)
            {
                producer.SpinWrite(Stopwatch.GetTimestamp());
                Thread.SpinWait(50); // Small delay
            }
        });

        var reader = new Thread(() =>
        {
            ready.Wait();
            for (int i = 0; i < count; i++)
            {
                long sentTime = consumer.SpinRead(0);
                long recvTime = Stopwatch.GetTimestamp();
                latencies[i] = recvTime - sentTime;
            }
        });

        writer.Start();
        reader.Start();
        ready.Set();
        writer.Join();
        reader.Join();

        ror.Stop();
        cts.Cancel();

        double ticksPerNs = Stopwatch.Frequency / 1_000_000_000.0;
        var latenciesNs = latencies.Select(t => t / ticksPerNs).OrderBy(x => x).ToArray();

        PrintLatencyResult("RingOfRings End-to-End Latency",
            latenciesNs.Min(),
            latenciesNs.Max(),
            latenciesNs.Average(),
            latenciesNs[(int)(count * 0.50)],
            latenciesNs[(int)(count * 0.99)]);
    }

    // ============================================================================
    // SCALING BENCHMARKS
    // ============================================================================

    private static void Benchmark_Scaling_Producers()
    {
        Console.WriteLine("  Scaling with number of producers (1 consumer, 1M total messages):\n");

        int[] producerCounts = { 1, 2, 4, 8, 16 };
        const int totalMessages = 1_000_000;

        foreach (int numProducers in producerCounts)
        {
            int messagesPerProducer = totalMessages / numProducers;

            var ror = new RingOfRings<long>();
            var producers = new RingBuffer<long>[numProducers];
            for (int i = 0; i < numProducers; i++)
                producers[i] = ror.AddProducer();
            var consumer = ror.AddConsumer();

            using var cts = new CancellationTokenSource();
            ror.Start(cts.Token);

            var sw = Stopwatch.StartNew();

            var writers = new Thread[numProducers];
            for (int p = 0; p < numProducers; p++)
            {
                int producerId = p;
                writers[p] = new Thread(() =>
                {
                    for (int i = 0; i < messagesPerProducer; i++)
                        producers[producerId].SpinWrite(i);
                });
            }

            var reader = new Thread(() =>
            {
                for (int i = 0; i < totalMessages; i++)
                    consumer.SpinRead(0);
            });

            foreach (var w in writers) w.Start();
            reader.Start();
            foreach (var w in writers) w.Join();
            reader.Join();

            sw.Stop();
            ror.Stop();
            cts.Cancel();

            PrintResult($"  {numProducers,2} producers", totalMessages, sw.Elapsed);
        }
    }

    private static void Benchmark_Scaling_Consumers()
    {
        Console.WriteLine("\n  Scaling with number of consumers (1 producer, 1M messages):\n");

        int[] consumerCounts = { 1, 2, 4, 8, 16 };
        const int totalMessages = 1_000_000;

        foreach (int numConsumers in consumerCounts)
        {
            var ror = new RingOfRings<long>();
            var producer = ror.AddProducer();
            var consumers = new RingBuffer<long>[numConsumers];
            for (int i = 0; i < numConsumers; i++)
                consumers[i] = ror.AddConsumer();

            using var cts = new CancellationTokenSource();
            ror.Start(cts.Token);

            var sw = Stopwatch.StartNew();

            var writer = new Thread(() =>
            {
                for (int i = 0; i < totalMessages; i++)
                    producer.SpinWrite(i);
            });

            var readers = new Thread[numConsumers];
            for (int c = 0; c < numConsumers; c++)
            {
                int consumerId = c;
                readers[c] = new Thread(() =>
                {
                    for (int i = 0; i < totalMessages; i++)
                        consumers[consumerId].SpinRead(0);
                });
            }

            writer.Start();
            foreach (var r in readers) r.Start();
            writer.Join();
            foreach (var r in readers) r.Join();

            sw.Stop();
            ror.Stop();
            cts.Cancel();

            PrintResult($"  {numConsumers,2} consumers", totalMessages, sw.Elapsed);
        }
    }

    private static void Benchmark_Scaling_MessageCount()
    {
        Console.WriteLine("\n  Scaling with message count (1P-1C):\n");

        int[] messageCounts = { 100_000, 500_000, 1_000_000, 5_000_000, 10_000_000 };

        foreach (int count in messageCounts)
        {
            var ror = new RingOfRings<long>();
            var producer = ror.AddProducer();
            var consumer = ror.AddConsumer();

            using var cts = new CancellationTokenSource();
            ror.Start(cts.Token);

            var sw = Stopwatch.StartNew();

            var writer = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                    producer.SpinWrite(i);
            });

            var reader = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                    consumer.SpinRead(0);
            });

            writer.Start();
            reader.Start();
            writer.Join();
            reader.Join();

            sw.Stop();
            ror.Stop();
            cts.Cancel();

            PrintResult($"  {count,10:N0} messages", count, sw.Elapsed);
        }
    }
}

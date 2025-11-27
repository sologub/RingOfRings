namespace RorCs.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Comprehensive test suite for RingOfRings implementation.
/// Tests focus on ordering guarantees, CPU cache coherency issues,
/// and high-volume stress testing with 1 million+ items.
/// </summary>
public static class RingOfRingsTests
{
    private static int _passed = 0;
    private static int _failed = 0;
    private static readonly object _lock = new();

    public static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          RingOfRings Comprehensive Test Suite                    ║");
        Console.WriteLine("║          Testing CPU Cache Coherency and Ordering                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var stopwatch = Stopwatch.StartNew();

        // Basic RingBuffer tests
        RunTest("RingBuffer_BasicWriteRead_MaintainsOrder", RingBuffer_BasicWriteRead_MaintainsOrder);
        RunTest("RingBuffer_SpinWriteRead_MaintainsOrder", RingBuffer_SpinWriteRead_MaintainsOrder);
        RunTest("RingBuffer_FullCapacity_HandlesCorrectly", RingBuffer_FullCapacity_HandlesCorrectly);
        RunTest("RingBuffer_EmptyRead_ReturnsFalse", RingBuffer_EmptyRead_ReturnsFalse);
        RunTest("RingBuffer_MultipleReaders_IndependentGates", RingBuffer_MultipleReaders_IndependentGates);

        // High-volume ordering tests
        RunTest("RingBuffer_1Million_ExactIncrementalOrder", RingBuffer_1Million_ExactIncrementalOrder);
        RunTest("RingBuffer_1Million_VerifyEveryItemIncreasesByOne", RingBuffer_1Million_VerifyEveryItemIncreasesByOne);

        // RingOfRings integration tests
        RunTest("RingOfRings_SingleProducerSingleConsumer_OrderPreserved", RingOfRings_SingleProducerSingleConsumer_OrderPreserved);
        RunTest("RingOfRings_SingleProducerMultipleConsumers_AllReceiveSameOrder", RingOfRings_SingleProducerMultipleConsumers_AllReceiveSameOrder);
        RunTest("RingOfRings_1Million_SingleProducerOrderPreserved", RingOfRings_1Million_SingleProducerOrderPreserved);
        RunTest("RingOfRings_1Million_IncrementalVerification", RingOfRings_1Million_IncrementalVerification);

        // Multi-producer tests (ordering within each producer)
        RunTest("RingOfRings_MultipleProducers_OrderWithinProducerPreserved", RingOfRings_MultipleProducers_OrderWithinProducerPreserved);
        RunTest("RingOfRings_MultipleProducers_NoItemsLost", RingOfRings_MultipleProducers_NoItemsLost);

        // CPU Cache stress tests
        RunTest("CPUCache_RapidWriteRead_NoStaleData", CPUCache_RapidWriteRead_NoStaleData);
        RunTest("CPUCache_ConcurrentAccess_NoCorruption", CPUCache_ConcurrentAccess_NoCorruption);
        RunTest("CPUCache_AlternatingWriteRead_Consistent", CPUCache_AlternatingWriteRead_Consistent);
        RunTest("CPUCache_HighContention_DataIntegrity", CPUCache_HighContention_DataIntegrity);

        // Edge cases
        RunTest("EdgeCase_WrapAround_OrderPreserved", EdgeCase_WrapAround_OrderPreserved);
        RunTest("EdgeCase_ExactCapacityBoundary_Works", EdgeCase_ExactCapacityBoundary_Works);
        RunTest("EdgeCase_RepeatedFillDrain_OrderPreserved", EdgeCase_RepeatedFillDrain_OrderPreserved);
        RunTest("EdgeCase_SmallBursts_Ordering", EdgeCase_SmallBursts_Ordering);

        // Memory ordering specific tests
        RunTest("MemoryOrdering_WriteThenRead_SameThread", MemoryOrdering_WriteThenRead_SameThread);
        RunTest("MemoryOrdering_CrossThread_Visibility", MemoryOrdering_CrossThread_Visibility);
        RunTest("MemoryOrdering_1Million_CrossThread", MemoryOrdering_1Million_CrossThread);

        // Stress tests
        RunTest("Stress_HighVolume_2Million_Items", Stress_HighVolume_2Million_Items);
        RunTest("Stress_MultipleProducersConsumers_1Million", Stress_MultipleProducersConsumers_1Million);

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  RESULTS: {_passed} passed, {_failed} failed                                       ║");
        Console.WriteLine($"║  Total time: {stopwatch.Elapsed}                               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");

        Environment.Exit(_failed > 0 ? 1 : 0);
    }

    private static void RunTest(string testName, Action testAction)
    {
        Console.Write($"  [{DateTime.Now:HH:mm:ss}] {testName}... ");
        var sw = Stopwatch.StartNew();
        try
        {
            testAction();
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"PASSED ({sw.ElapsedMilliseconds}ms)");
            Console.ResetColor();
            Interlocked.Increment(ref _passed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"    Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"    Inner: {ex.InnerException.Message}");
            Console.ResetColor();
            Interlocked.Increment(ref _failed);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message) where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            throw new Exception($"{message} - Expected: {expected}, Actual: {actual}");
    }

    // ============================================================================
    // BASIC RINGBUFFER TESTS
    // ============================================================================

    private static void RingBuffer_BasicWriteRead_MaintainsOrder()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        // Write 100 items
        for (int i = 1; i <= 100; i++)
        {
            Assert(buffer.Write(i), $"Write failed for item {i}");
        }

        // Read and verify order
        for (int i = 1; i <= 100; i++)
        {
            Assert(buffer.Read(0, out int value), $"Read failed for item {i}");
            AssertEqual(i, value, $"Order mismatch at position {i}");
        }
    }

    private static void RingBuffer_SpinWriteRead_MaintainsOrder()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        // Use SpinWrite for 100 items
        for (int i = 1; i <= 100; i++)
        {
            buffer.SpinWrite(i);
        }

        // Use SpinRead and verify
        for (int i = 1; i <= 100; i++)
        {
            int value = buffer.SpinRead(0);
            AssertEqual(i, value, $"SpinRead order mismatch at position {i}");
        }
    }

    private static void RingBuffer_FullCapacity_HandlesCorrectly()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(16, 1, signal); // Small capacity for quick test

        // Fill to near capacity (capacity - 2 due to IsFull logic)
        int written = 0;
        for (int i = 1; i <= 16; i++)
        {
            if (buffer.Write(i))
                written++;
            else
                break;
        }

        // Should have written capacity - 2 items
        Assert(written == 14, $"Expected to write 14 items, but wrote {written}");
        Assert(buffer.IsFull, "Buffer should be full");
    }

    private static void RingBuffer_EmptyRead_ReturnsFalse()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        Assert(buffer.IsEmpty(0), "New buffer should be empty");
        Assert(!buffer.Read(0, out _), "Read from empty buffer should return false");
    }

    private static void RingBuffer_MultipleReaders_IndependentGates()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 3, signal); // 3 readers

        // Write 10 items
        for (int i = 1; i <= 10; i++)
        {
            buffer.Write(i);
        }

        // Reader 0 reads all
        for (int i = 1; i <= 10; i++)
        {
            Assert(buffer.Read(0, out int v0), $"Reader 0 read failed at {i}");
            AssertEqual(i, v0, $"Reader 0 order mismatch at {i}");
        }

        // Reader 1 should still see all items
        for (int i = 1; i <= 10; i++)
        {
            Assert(buffer.Read(1, out int v1), $"Reader 1 read failed at {i}");
            AssertEqual(i, v1, $"Reader 1 order mismatch at {i}");
        }

        // Reader 2 should also see all items
        for (int i = 1; i <= 10; i++)
        {
            Assert(buffer.Read(2, out int v2), $"Reader 2 read failed at {i}");
            AssertEqual(i, v2, $"Reader 2 order mismatch at {i}");
        }
    }

    // ============================================================================
    // HIGH-VOLUME ORDERING TESTS (1 MILLION ITEMS)
    // ============================================================================

    private static void RingBuffer_1Million_ExactIncrementalOrder()
    {
        const int count = 1_000_000;
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        var errors = new ConcurrentBag<string>();
        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != i)
                {
                    errors.Add($"Position {i}: expected {i}, got {value}");
                    if (errors.Count >= 100) break; // Limit error collection
                }
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (errors.Count > 0)
        {
            throw new Exception($"Found {errors.Count} ordering errors. First few: {string.Join(", ", errors.Take(5))}");
        }
    }

    private static void RingBuffer_1Million_VerifyEveryItemIncreasesByOne()
    {
        const int count = 1_000_000;
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        int errorCount = 0;
        int lastValue = 0;
        int firstErrorAt = -1;
        int expectedAtFirstError = -1;
        int actualAtFirstError = -1;

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = buffer.SpinRead(0);
                int expected = lastValue + 1;
                if (value != expected)
                {
                    if (errorCount == 0)
                    {
                        firstErrorAt = i;
                        expectedAtFirstError = expected;
                        actualAtFirstError = value;
                    }
                    errorCount++;
                }
                lastValue = value;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (errorCount > 0)
        {
            throw new Exception($"Found {errorCount} incremental errors. First error at position {firstErrorAt}: expected {expectedAtFirstError}, got {actualAtFirstError}");
        }
    }

    // ============================================================================
    // RINGOFRING INTEGRATION TESTS
    // ============================================================================

    private static void RingOfRings_SingleProducerSingleConsumer_OrderPreserved()
    {
        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int count = 10000;
        var errors = new ConcurrentBag<string>();

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                producer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = consumer.SpinRead(0);
                if (value != i)
                {
                    errors.Add($"Position {i}: expected {i}, got {value}");
                }
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        if (errors.Count > 0)
        {
            throw new Exception($"Found {errors.Count} ordering errors: {string.Join(", ", errors.Take(5))}");
        }
    }

    private static void RingOfRings_SingleProducerMultipleConsumers_AllReceiveSameOrder()
    {
        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumers = new RingBuffer<int>[5];
        for (int i = 0; i < 5; i++)
        {
            consumers[i] = ror.AddConsumer();
        }

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int count = 10000;
        var errorsByConsumer = new ConcurrentDictionary<int, ConcurrentBag<string>>();

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                producer.SpinWrite(i);
            }
        });

        var readerThreads = new Thread[5];
        for (int c = 0; c < 5; c++)
        {
            int consumerId = c;
            errorsByConsumer[consumerId] = new ConcurrentBag<string>();
            readerThreads[c] = new Thread(() =>
            {
                for (int i = 1; i <= count; i++)
                {
                    int value = consumers[consumerId].SpinRead(0);
                    if (value != i)
                    {
                        errorsByConsumer[consumerId].Add($"Consumer {consumerId} pos {i}: expected {i}, got {value}");
                    }
                }
            });
        }

        writerThread.Start();
        foreach (var t in readerThreads) t.Start();

        writerThread.Join();
        foreach (var t in readerThreads) t.Join();

        ror.Stop();
        cts.Cancel();

        var allErrors = errorsByConsumer.Values.SelectMany(e => e).ToList();
        if (allErrors.Count > 0)
        {
            throw new Exception($"Found {allErrors.Count} errors across consumers: {string.Join(", ", allErrors.Take(5))}");
        }
    }

    private static void RingOfRings_1Million_SingleProducerOrderPreserved()
    {
        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int count = 1_000_000;
        int errorCount = 0;
        int firstErrorAt = -1;
        int expectedAtFirstError = -1;
        int actualAtFirstError = -1;

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                producer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = consumer.SpinRead(0);
                if (value != i)
                {
                    if (Interlocked.Increment(ref errorCount) == 1)
                    {
                        firstErrorAt = i;
                        expectedAtFirstError = i;
                        actualAtFirstError = value;
                    }
                }
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        if (errorCount > 0)
        {
            throw new Exception($"Found {errorCount} ordering errors in 1M items. First at position {firstErrorAt}: expected {expectedAtFirstError}, got {actualAtFirstError}");
        }
    }

    private static void RingOfRings_1Million_IncrementalVerification()
    {
        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int count = 1_000_000;
        int lastValue = 0;
        int nonIncrementalCount = 0;
        int firstNonIncrementalAt = -1;
        int prevValueAtError = -1;
        int curValueAtError = -1;

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                producer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = consumer.SpinRead(0);
                if (value != lastValue + 1)
                {
                    if (Interlocked.Increment(ref nonIncrementalCount) == 1)
                    {
                        firstNonIncrementalAt = i;
                        prevValueAtError = lastValue;
                        curValueAtError = value;
                    }
                }
                lastValue = value;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        if (nonIncrementalCount > 0)
        {
            throw new Exception($"Found {nonIncrementalCount} non-incremental values. First at position {firstNonIncrementalAt}: prev={prevValueAtError}, cur={curValueAtError}, expected cur={prevValueAtError + 1}");
        }
    }

    // ============================================================================
    // MULTI-PRODUCER TESTS
    // ============================================================================

    private static void RingOfRings_MultipleProducers_OrderWithinProducerPreserved()
    {
        var ror = new RingOfRings<long>();
        var producers = new RingBuffer<long>[4];
        for (int i = 0; i < 4; i++)
        {
            producers[i] = ror.AddProducer();
        }
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int countPerProducer = 50000;

        // Each producer sends values encoded as: (producerId << 20) | sequenceNumber
        var producerThreads = new Thread[4];
        for (int p = 0; p < 4; p++)
        {
            int producerId = p;
            producerThreads[p] = new Thread(() =>
            {
                for (int i = 1; i <= countPerProducer; i++)
                {
                    long encoded = ((long)producerId << 20) | i;
                    producers[producerId].SpinWrite(encoded);
                }
            });
        }

        var receivedByProducer = new ConcurrentDictionary<int, List<int>>();
        for (int i = 0; i < 4; i++) receivedByProducer[i] = new List<int>();

        var readerThread = new Thread(() =>
        {
            int totalExpected = countPerProducer * 4;
            for (int i = 0; i < totalExpected; i++)
            {
                long encoded = consumer.SpinRead(0);
                int producerId = (int)(encoded >> 20);
                int seq = (int)(encoded & 0xFFFFF);
                lock (receivedByProducer[producerId])
                {
                    receivedByProducer[producerId].Add(seq);
                }
            }
        });

        foreach (var t in producerThreads) t.Start();
        readerThread.Start();

        foreach (var t in producerThreads) t.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        // Verify ordering within each producer
        foreach (var kvp in receivedByProducer)
        {
            var sequences = kvp.Value;
            for (int i = 1; i < sequences.Count; i++)
            {
                if (sequences[i] <= sequences[i - 1])
                {
                    throw new Exception($"Producer {kvp.Key} order violated: position {i-1}={sequences[i-1]}, position {i}={sequences[i]}");
                }
            }
        }
    }

    private static void RingOfRings_MultipleProducers_NoItemsLost()
    {
        var ror = new RingOfRings<int>();
        var producers = new RingBuffer<int>[3];
        for (int i = 0; i < 3; i++)
        {
            producers[i] = ror.AddProducer();
        }
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int countPerProducer = 100000;
        var received = new ConcurrentBag<int>();

        var producerThreads = new Thread[3];
        for (int p = 0; p < 3; p++)
        {
            int producerId = p;
            int baseValue = producerId * countPerProducer;
            producerThreads[p] = new Thread(() =>
            {
                for (int i = 1; i <= countPerProducer; i++)
                {
                    producers[producerId].SpinWrite(baseValue + i);
                }
            });
        }

        var readerThread = new Thread(() =>
        {
            int totalExpected = countPerProducer * 3;
            for (int i = 0; i < totalExpected; i++)
            {
                int value = consumer.SpinRead(0);
                received.Add(value);
            }
        });

        foreach (var t in producerThreads) t.Start();
        readerThread.Start();

        foreach (var t in producerThreads) t.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        // Verify no items lost
        var sorted = received.OrderBy(x => x).ToList();
        AssertEqual(countPerProducer * 3, sorted.Count, "Total items received mismatch");

        // Check each producer's range
        for (int p = 0; p < 3; p++)
        {
            int baseValue = p * countPerProducer;
            for (int i = 1; i <= countPerProducer; i++)
            {
                int expected = baseValue + i;
                if (!sorted.Contains(expected))
                {
                    throw new Exception($"Missing value {expected} from producer {p}");
                }
            }
        }
    }

    // ============================================================================
    // CPU CACHE STRESS TESTS
    // ============================================================================

    private static void CPUCache_RapidWriteRead_NoStaleData()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<long>(1024, 1, signal);

        const int iterations = 500000;
        long writeTimestamp = 0;
        int staleDataCount = 0;

        var writerThread = new Thread(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                // Write current timestamp as value
                Interlocked.Exchange(ref writeTimestamp, Stopwatch.GetTimestamp());
                buffer.SpinWrite(writeTimestamp);
            }
        });

        var readerThread = new Thread(() =>
        {
            long lastRead = 0;
            for (int i = 0; i < iterations; i++)
            {
                long value = buffer.SpinRead(0);
                // Value should never decrease (stale data would show older timestamp)
                if (value < lastRead)
                {
                    Interlocked.Increment(ref staleDataCount);
                }
                lastRead = value;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (staleDataCount > 0)
        {
            throw new Exception($"Detected {staleDataCount} instances of potentially stale data (decreasing timestamps)");
        }
    }

    private static void CPUCache_ConcurrentAccess_NoCorruption()
    {
        var signal = new ManualResetEventSlim(false);
        // Use larger struct to potentially expose partial writes
        var buffer = new RingBuffer<Guid>(1024, 1, signal);

        const int iterations = 100000;
        var writtenGuids = new ConcurrentBag<Guid>();
        var readGuids = new ConcurrentBag<Guid>();

        var writerThread = new Thread(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var guid = Guid.NewGuid();
                writtenGuids.Add(guid);
                buffer.SpinWrite(guid);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var guid = buffer.SpinRead(0);
                readGuids.Add(guid);
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        // Verify all read GUIDs were actually written (no corruption)
        var writtenSet = new HashSet<Guid>(writtenGuids);
        var invalidGuids = readGuids.Where(g => !writtenSet.Contains(g)).ToList();

        if (invalidGuids.Count > 0)
        {
            throw new Exception($"Found {invalidGuids.Count} corrupted/invalid GUIDs that were never written");
        }
    }

    private static void CPUCache_AlternatingWriteRead_Consistent()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        const int iterations = 200000;
        var errors = new ConcurrentBag<string>();
        var barrier = new Barrier(2);

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= iterations; i++)
            {
                buffer.SpinWrite(i);
                if (i % 1000 == 0) barrier.SignalAndWait(); // Sync point
            }
        });

        var readerThread = new Thread(() =>
        {
            int expected = 1;
            for (int i = 1; i <= iterations; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != expected)
                {
                    errors.Add($"At {i}: expected {expected}, got {value}");
                }
                expected = value + 1;
                if (i % 1000 == 0) barrier.SignalAndWait();
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (errors.Count > 0)
        {
            throw new Exception($"Found {errors.Count} consistency errors: {string.Join(", ", errors.Take(5))}");
        }
    }

    private static void CPUCache_HighContention_DataIntegrity()
    {
        var ror = new RingOfRings<int>();

        // Create many producers to cause high contention
        var producers = new RingBuffer<int>[8];
        for (int i = 0; i < 8; i++)
        {
            producers[i] = ror.AddProducer();
        }

        var consumers = new RingBuffer<int>[4];
        for (int i = 0; i < 4; i++)
        {
            consumers[i] = ror.AddConsumer();
        }

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        const int countPerProducer = 25000;
        var allReceived = new ConcurrentBag<int>[4];
        for (int i = 0; i < 4; i++) allReceived[i] = new ConcurrentBag<int>();

        var producerThreads = new Thread[8];
        for (int p = 0; p < 8; p++)
        {
            int producerId = p;
            int baseValue = producerId * countPerProducer;
            producerThreads[p] = new Thread(() =>
            {
                for (int i = 1; i <= countPerProducer; i++)
                {
                    producers[producerId].SpinWrite(baseValue + i);
                    // Add some variability to create contention
                    if (i % 100 == 0) Thread.SpinWait(10);
                }
            });
        }

        var readerThreads = new Thread[4];
        for (int c = 0; c < 4; c++)
        {
            int consumerId = c;
            readerThreads[c] = new Thread(() =>
            {
                int totalExpected = countPerProducer * 8;
                for (int i = 0; i < totalExpected; i++)
                {
                    int value = consumers[consumerId].SpinRead(0);
                    allReceived[consumerId].Add(value);
                }
            });
        }

        foreach (var t in producerThreads) t.Start();
        foreach (var t in readerThreads) t.Start();

        foreach (var t in producerThreads) t.Join();
        foreach (var t in readerThreads) t.Join();

        ror.Stop();
        cts.Cancel();

        // Verify each consumer received all items
        for (int c = 0; c < 4; c++)
        {
            var received = allReceived[c].OrderBy(x => x).ToList();
            AssertEqual(countPerProducer * 8, received.Count, $"Consumer {c} item count mismatch");
        }

        // Verify all consumers received the same set
        var firstSet = new HashSet<int>(allReceived[0]);
        for (int c = 1; c < 4; c++)
        {
            var otherSet = new HashSet<int>(allReceived[c]);
            if (!firstSet.SetEquals(otherSet))
            {
                throw new Exception($"Consumer {c} received different items than consumer 0");
            }
        }
    }

    // ============================================================================
    // EDGE CASE TESTS
    // ============================================================================

    private static void EdgeCase_WrapAround_OrderPreserved()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(64, 1, signal); // Small buffer to force many wraparounds

        const int iterations = 10000; // Many more than capacity
        var errors = new ConcurrentBag<string>();

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= iterations; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= iterations; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != i)
                {
                    errors.Add($"Position {i}: expected {i}, got {value}");
                }
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (errors.Count > 0)
        {
            throw new Exception($"Wraparound caused {errors.Count} ordering errors: {string.Join(", ", errors.Take(5))}");
        }
    }

    private static void EdgeCase_ExactCapacityBoundary_Works()
    {
        var signal = new ManualResetEventSlim(false);
        const int capacity = 128;
        var buffer = new RingBuffer<int>(capacity, 1, signal);

        // Fill and drain exactly at capacity boundaries multiple times
        for (int round = 0; round < 100; round++)
        {
            int baseValue = round * 1000;

            // Fill to near capacity
            for (int i = 1; i <= capacity - 2; i++)
            {
                Assert(buffer.Write(baseValue + i), $"Round {round}: Write failed at {i}");
            }

            // Drain completely
            for (int i = 1; i <= capacity - 2; i++)
            {
                Assert(buffer.Read(0, out int value), $"Round {round}: Read failed at {i}");
                AssertEqual(baseValue + i, value, $"Round {round}: Value mismatch at {i}");
            }

            Assert(buffer.IsEmpty(0), $"Round {round}: Buffer should be empty after drain");
        }
    }

    private static void EdgeCase_RepeatedFillDrain_OrderPreserved()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(256, 1, signal);

        for (int cycle = 0; cycle < 1000; cycle++)
        {
            int batchSize = (cycle % 200) + 10; // Variable batch sizes
            int baseValue = cycle * 1000;

            for (int i = 1; i <= batchSize; i++)
            {
                buffer.SpinWrite(baseValue + i);
            }

            for (int i = 1; i <= batchSize; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != baseValue + i)
                {
                    throw new Exception($"Cycle {cycle}, pos {i}: expected {baseValue + i}, got {value}");
                }
            }
        }
    }

    private static void EdgeCase_SmallBursts_Ordering()
    {
        var ror = new RingOfRings<int>();
        var producer = ror.AddProducer();
        var consumer = ror.AddConsumer();

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        int globalSeq = 0;
        var errors = new ConcurrentBag<string>();

        var writerThread = new Thread(() =>
        {
            for (int burst = 0; burst < 10000; burst++)
            {
                int burstSize = (burst % 10) + 1;
                for (int i = 0; i < burstSize; i++)
                {
                    producer.SpinWrite(++globalSeq);
                }
                Thread.SpinWait(burst % 5); // Variable delays
            }
        });

        var readerThread = new Thread(() =>
        {
            int expected = 1;
            int totalExpected = 0;
            for (int burst = 0; burst < 10000; burst++)
            {
                totalExpected += (burst % 10) + 1;
            }

            for (int i = 0; i < totalExpected; i++)
            {
                int value = consumer.SpinRead(0);
                if (value != expected)
                {
                    errors.Add($"Position {i}: expected {expected}, got {value}");
                }
                expected++;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        ror.Stop();
        cts.Cancel();

        if (errors.Count > 0)
        {
            throw new Exception($"Small bursts caused {errors.Count} errors: {string.Join(", ", errors.Take(5))}");
        }
    }

    // ============================================================================
    // MEMORY ORDERING TESTS
    // ============================================================================

    private static void MemoryOrdering_WriteThenRead_SameThread()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        // Same-thread ordering should always work
        for (int i = 1; i <= 100000; i++)
        {
            buffer.Write(i);
            Assert(buffer.Read(0, out int value), $"Read failed after write at {i}");
            AssertEqual(i, value, $"Same-thread ordering failed at {i}");
        }
    }

    private static void MemoryOrdering_CrossThread_Visibility()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(1024, 1, signal);

        const int iterations = 500000;
        int visibilityErrors = 0;

        // Test that writes become visible to reader in correct order
        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= iterations; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            int lastSeen = 0;
            for (int i = 0; i < iterations; i++)
            {
                int value = buffer.SpinRead(0);
                // Due to memory ordering issues, we might see values out of order
                if (value < lastSeen)
                {
                    Interlocked.Increment(ref visibilityErrors);
                }
                // Also check that values are reasonable (not garbage)
                if (value <= 0 || value > iterations)
                {
                    Interlocked.Increment(ref visibilityErrors);
                }
                lastSeen = value;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (visibilityErrors > 0)
        {
            throw new Exception($"Found {visibilityErrors} memory ordering/visibility errors");
        }
    }

    private static void MemoryOrdering_1Million_CrossThread()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(2048, 1, signal);

        const int count = 1_000_000;
        int orderingErrors = 0;
        int firstErrorIdx = -1;
        int firstErrorExpected = -1;
        int firstErrorActual = -1;

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            int expected = 1;
            for (int i = 1; i <= count; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != expected)
                {
                    if (Interlocked.Increment(ref orderingErrors) == 1)
                    {
                        firstErrorIdx = i;
                        firstErrorExpected = expected;
                        firstErrorActual = value;
                    }
                }
                expected++;
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (orderingErrors > 0)
        {
            throw new Exception($"Memory ordering test found {orderingErrors} errors. First at index {firstErrorIdx}: expected {firstErrorExpected}, got {firstErrorActual}");
        }
    }

    // ============================================================================
    // STRESS TESTS
    // ============================================================================

    private static void Stress_HighVolume_2Million_Items()
    {
        var signal = new ManualResetEventSlim(false);
        var buffer = new RingBuffer<int>(4096, 1, signal);

        const int count = 2_000_000;
        int errorCount = 0;
        int firstErrorAt = -1;

        var writerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                buffer.SpinWrite(i);
            }
        });

        var readerThread = new Thread(() =>
        {
            for (int i = 1; i <= count; i++)
            {
                int value = buffer.SpinRead(0);
                if (value != i)
                {
                    if (Interlocked.Increment(ref errorCount) == 1)
                    {
                        firstErrorAt = i;
                    }
                }
            }
        });

        writerThread.Start();
        readerThread.Start();

        writerThread.Join();
        readerThread.Join();

        if (errorCount > 0)
        {
            throw new Exception($"2M stress test: {errorCount} ordering errors, first at position {firstErrorAt}");
        }
    }

    private static void Stress_MultipleProducersConsumers_1Million()
    {
        var ror = new RingOfRings<long>();

        const int numProducers = 5;
        const int numConsumers = 5;
        const int itemsPerProducer = 200000;

        var producers = new RingBuffer<long>[numProducers];
        for (int i = 0; i < numProducers; i++)
        {
            producers[i] = ror.AddProducer();
        }

        var consumers = new RingBuffer<long>[numConsumers];
        for (int i = 0; i < numConsumers; i++)
        {
            consumers[i] = ror.AddConsumer();
        }

        using var cts = new CancellationTokenSource();
        ror.Start(cts.Token);

        // Track received items per consumer (each consumer writes only to its own list)
        var receivedByConsumer = new List<long>[numConsumers];
        for (int i = 0; i < numConsumers; i++)
        {
            receivedByConsumer[i] = new List<long>();
        }

        var producerThreads = new Thread[numProducers];
        for (int p = 0; p < numProducers; p++)
        {
            int producerId = p;
            producerThreads[p] = new Thread(() =>
            {
                for (int i = 1; i <= itemsPerProducer; i++)
                {
                    // Encode producer ID and sequence
                    long encoded = ((long)producerId << 32) | (uint)i;
                    producers[producerId].SpinWrite(encoded);
                }
            });
        }

        var consumerThreads = new Thread[numConsumers];
        for (int c = 0; c < numConsumers; c++)
        {
            int consumerId = c;
            consumerThreads[c] = new Thread(() =>
            {
                int totalExpected = numProducers * itemsPerProducer;
                for (int i = 0; i < totalExpected; i++)
                {
                    long value = consumers[consumerId].SpinRead(0);
                    receivedByConsumer[consumerId].Add(value);
                }
            });
        }

        foreach (var t in producerThreads) t.Start();
        foreach (var t in consumerThreads) t.Start();

        foreach (var t in producerThreads) t.Join();
        foreach (var t in consumerThreads) t.Join();

        ror.Stop();
        cts.Cancel();

        // Verify each consumer received everything
        int expectedTotal = numProducers * itemsPerProducer;
        for (int c = 0; c < numConsumers; c++)
        {
            AssertEqual(expectedTotal, receivedByConsumer[c].Count,
                $"Consumer {c} received wrong count");
        }

        // Verify ordering within each producer stream (for first consumer)
        var firstConsumerItems = receivedByConsumer[0];
        var byProducer = new Dictionary<int, List<int>>();
        for (int p = 0; p < numProducers; p++) byProducer[p] = new List<int>();

        foreach (var encoded in firstConsumerItems)
        {
            int producerId = (int)(encoded >> 32);
            int seq = (int)(encoded & 0xFFFFFFFF);
            byProducer[producerId].Add(seq);
        }

        foreach (var kvp in byProducer)
        {
            var seqs = kvp.Value;
            for (int i = 1; i < seqs.Count; i++)
            {
                if (seqs[i] <= seqs[i - 1])
                {
                    throw new Exception($"Producer {kvp.Key} ordering violated at consumer position {i}: {seqs[i-1]} -> {seqs[i]}");
                }
            }
        }
    }
}

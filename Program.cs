// Example on how to use RingOfRings

using RorCs;

var ror = new RingOfRings<int>();

const int consumersCount = 10;
const int producersCount = 10;
const int count = 1000000; // number of published test events

var consumers = new RingBuffer<int>[consumersCount];
for (int i = 0; i < consumersCount; i++)
{
    consumers[i] = ror.AddConsumer();
}

var producers = new RingBuffer<int>[producersCount];
for (int i = 0; i < producersCount; i++)
{
    producers[i] = ror.AddProducer();
}

var t1 = new Thread(() =>
    {
        int n = 1;
        for (int i = 1; i <= count; i++)
        {
            producers[(producersCount - 1) % i].SpinWrite(n);
            n++;
        }
    })
    { IsBackground = true };

t1.Start();

ror.Start(CancellationToken.None);

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

int ev = 0;
for (int i = 1; i <= count; i++)
{
    foreach (var consumer in consumers)
    {
        ev = consumer.SpinRead(0);
    }
}

Console.WriteLine(ev);

stopwatch.Stop();
Console.WriteLine($"Total runtime: {stopwatch.Elapsed}");
# RingOfRings

## Overview
**RingOfRings** is a high-performance, multi-producer, multi-consumer ring buffer system implemented in C#. It is designed to facilitate efficient message passing and data synchronization between producers and consumers in concurrent environments. The implementation leverages `ManualResetEventSlim` for efficient signaling and thread synchronization, providing stable and predictable latency for data processing.

This project is especially useful for scenarios involving multiple producers feeding data into a central buffer, with multiple consumers reading from it. It aims to provide a robust solution for building concurrent systems where low-latency message transfer is a key requirement.

## Features
- **Multi-Producer, Multi-Consumer**: Supports multiple producers and consumers with a single shared lock-free main ring buffer.
- **Concurrency Optimizations**: Uses `ManualResetEventSlim` for signaling and synchronization to avoid latency issues caused by context switching.
- **Low Latency**: Designed to minimize variability in message passing latency.
- **Written in C#**: Fully implemented in C#, ideal for use in .NET environments.

## Requirements
- .NET 6.0 or later.
- C# compiler (Visual Studio, Rider, or equivalent).

## Installation
To include this project in your own .NET solution:
1. Clone this repository:
   ```sh
   git clone https://github.com/sologub/RingOfRings.git
   ```
2. Add the project to your solution in Visual Studio or any other IDE:
   - Open your existing solution.
   - Add the cloned project to the solution.
   - Reference the `RingOfRings` project from any project where you need to use it.

## Usage
Below is a brief example of how you can use the `RingOfRings` class in your application:

```csharp
using System;
using System.Threading;
using SeriousBit.NetBalancer.Core.Utils;

public class Program
{
    public static void Main()
    {
        var ringOfRings = new RingOfRings<int>();
        CancellationTokenSource cts = new CancellationTokenSource();

        // Add producers and consumers
        var producer = ringOfRings.AddProducer();
        var consumer = ringOfRings.AddConsumer();

        // Start the RingOfRings system
        ringOfRings.Start(cts.Token);

        // Example of producing data
        producer.Write(42);

        // Example of consuming data
        if (consumer.Read(0, out var value))
        {
            Console.WriteLine($"Consumed: {value}");
        }

        // Stop the system when finished
        ringOfRings.Stop();
    }
}
```

## Contributing
Contributions are welcome! If you have ideas for features or improvements, feel free to open an issue or submit a pull request. Please adhere to the project's coding standards and submit detailed descriptions of your changes.

### Reporting Issues
If you encounter any bugs or have questions, please open an issue in the GitHub repository with detailed information so that others can help.

## License
This project is licensed under the MIT License. You are free to use, modify, and distribute this software as long as you include the original copyright notice.

For more details, see the [LICENSE](LICENSE) file.

## Acknowledgments
- Inspired by typical concurrent programming challenges in high-performance messaging systems.
- Special thanks to the open-source community for guidance and inspiration.

## Contact
Feel free to contact me if you have questions or suggestions.

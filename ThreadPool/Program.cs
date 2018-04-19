using System;
using System.Threading;

namespace ThreadPool
{
    class Program
    {
        static void Main(string[] args)
        {
            const int FibonacciCalculations = 10;

            // One event is used for each Fibonacci object.  
            ManualResetEvent[] doneEvents = new ManualResetEvent[FibonacciCalculations];
            Fibonacci[] fibArray = new Fibonacci[FibonacciCalculations];
            Random random = new Random();

            // Configure and start threads using ThreadPool.  
            for (int i = 0; i < FibonacciCalculations; i++)
            {
                doneEvents[i] = new ManualResetEvent(false);
                Fibonacci f = new Fibonacci(random.Next(25, 45), doneEvents[i]);
                fibArray[i] = f;
              
                ThreadPool.QueueUserWorkItem(f.ThreadPoolCallback);

            }

            // Wait for all threads in pool to calculate.  
            WaitHandle.WaitAll(doneEvents);
            Console.WriteLine("All calculations are complete.");

            // Display the results.  
            for (int i = 0; i < FibonacciCalculations; i++)
            {
                Fibonacci f = fibArray[i];
                Console.WriteLine("Fibonacci({0}) = {1}", f.N, f.FibOfN);
            }

            Console.Read();
        }

    }
 }
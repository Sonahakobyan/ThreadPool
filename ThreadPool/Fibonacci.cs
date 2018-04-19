using System;
using System.Threading;

namespace ThreadPool
{
    public class Fibonacci
    {
        private int n;
        private int fibOfN;
        private ManualResetEvent doneEvent;

        public int N { get { return n; } }
        public int FibOfN { get { return fibOfN; } }

         
        public Fibonacci(int n, ManualResetEvent doneEvent)
        {
            this.n = n;
            this.doneEvent = doneEvent;
        }


        /// <summary>
        /// Wrapper method for use with thread pool
        /// </summary>
        /// <param name="threadContext">Info about thread</param>
        public void ThreadPoolCallback(Object threadContext)
        {
            fibOfN = Calculate(n);
            doneEvent.Set();
        }



        /// <summary>
        /// Recursive method that calculates the Nth Fibonacci number
        /// </summary>
        /// <param name="n">The index</param>
        /// <returns></returns>
        public int Calculate(int n)
        {
            if (n <= 1)
            {
                return n;
            }

            return Calculate(n - 1) + Calculate(n - 2);
        }
    }
}

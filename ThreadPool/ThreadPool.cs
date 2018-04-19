using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ThreadPool
{
    class ThreadPool
    {
        public static readonly int MaxThreads = 3;
        public static int FinishedThreads = 0;
        public static int TotalQueued = 0;
        public static int UsedThreads = 0;

        private static ConcurrentQueue<WaitCallback> queue = new ConcurrentQueue<WaitCallback>();
        private static List<Thread> workers = new List<Thread>(MaxThreads);


        private static Thread backgroundThread = new Thread(() =>
        {
            while (true)
            {
                DequeueRun();
                Thread.Sleep(50);
            }
        });

        /// <summary>
        ///  Queues a method for execution, and specifies an object containing data to be used by the method.
        ///  The method executes when a thread pool thread becomes available.
        /// </summary>
        /// <param name="callBack">A System.Threading.WaitCallback representing the method to execute.</param>
        /// <returns> True if the method is successfully queued.
        /// System.NotSupportedException is thrown if the work item could not be queued.</returns>
        public static Boolean QueueUserWorkItem(WaitCallback callBack)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException("callBack is null.");
            }

           
            lock (queue)
            {
                queue.Enqueue(callBack);
                TotalQueued++;
            }

            lock (backgroundThread)
            {
                if (backgroundThread.ThreadState == ThreadState.Unstarted)
                {
                    backgroundThread.IsBackground = true;
                    backgroundThread.Start();
                }
            }
            return true;
        }


        private static void DequeueRun()
        {
            WaitCallback wcb = null;
            Thread stoppedThread = null;

            lock (workers)
            {
                if (FinishedThreads > 0)
                { 
                    //there may be threads need to be renewed / removed
                    foreach (var worker in workers)
                    {
                        if (worker.ThreadState == ThreadState.Stopped)
                        {
                            //get a reference to the stopped thread
                            stoppedThread = worker; 
                            break;
                        }
                    }
                }

                //reassign the maximum threads used
                if (workers.Count > UsedThreads) 
                {
                    UsedThreads = workers.Count;
                }
                lock (queue)
                {
                    if (queue.Count > 0 && MaxThreads - workers.Count > 0)
                    {
                        //if there's still work, dequeue something
                        queue.TryDequeue(out wcb); 
                    }
                }
            }
            
            //dequeued nothing and there exist a stopped thread
            if (wcb == null && stoppedThread != null) 
            {
                bool empty = false;

                //find out if the queue is empty
                lock (queue) 
                {
                    empty = queue.Count == 0;
                }
                if (empty)
                {
                    lock (workers)
                    {
                        if (!workers.Remove(stoppedThread))
                        {
                            throw new SystemException("Could not remove a thread from the list!");
                        }
                        else
                        {
                            Interlocked.Decrement(ref FinishedThreads);
                        }
                    }
                }
            }
            else if (wcb != null)
            {
                Thread thread = new Thread(new ParameterizedThreadStart(ThreadFunc));

                // thread will not keep an application running after all foreground threads have exited
                thread.IsBackground = true;
                lock (workers)
                {
                    if (MaxThreads - workers.Count > 0)
                    {
                        //replace the stopped thread with a new one
                        if (stoppedThread != null) 
                        {
                            workers[workers.IndexOf(stoppedThread)] = thread;
                        }
                        else
                        {
                            workers.Add(thread);
                        }
                    }
                    else
                    {
                        // let other active threads do the work.
                        lock (queue) 
                        {
                            queue.Enqueue(wcb);
                            wcb = null;
                        }
                    }
                }
                if (wcb != null)
                {
                    thread.Start(wcb);
                }
            }
        }


        public static void ThreadFunc(Object obj)
        {
            //when a thread is started, it already has a job assigned to it.
            WaitCallback wcb = obj as WaitCallback;
            if (wcb == null)
            {
                throw new ArgumentException("ThreadFunc must recieve WaitCallback as a parameter!");
            }
            wcb.Invoke(null);

            //from now on, I'm dequeueing/invoking jobs from the queue.
            while (true) 
            {
                wcb = null;

                lock (workers)
                {
                    lock (queue)
                    {
                        //help a non-empty queue to get rid of its load
                        if (queue.Count > 0) 
                        {
                            queue.TryDequeue(out wcb);
                        }
                    }
                }

                if (wcb != null)
                {
                    wcb.Invoke(null);
                }
                else
                {
                    //could not dequeue from the queue, terminate the thread
                    Interlocked.Increment(ref FinishedThreads);
                    return;
                }

                //context switch
                Thread.Sleep(0); 
            }
        }
    }
}

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
        private static List<Thread> threads = new List<Thread>(MaxThreads);


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


        /// <summary>
        /// Enqueue a thread if there is a task to do and remove stopped thread if it exists.
        /// Newly added thread will run only if there is free place for it.
        /// </summary>
        private static void DequeueRun()
        {
            WaitCallback task = null;
            Thread stoppedThread = null;

            lock (threads)
            {
                if (FinishedThreads > 0)
                { 
                    // there may be threads need to be renewed / removed
                    foreach (var thread in threads)
                    {
                        if (thread.ThreadState == ThreadState.Stopped)
                        {
                            // get a reference to the stopped thread
                            stoppedThread = thread; 
                            break;
                        }
                    }
                }

                // reassign the used threads count
                if (threads.Count > UsedThreads) 
                {
                    UsedThreads = threads.Count;
                }

                lock (queue)
                {
                    if (queue.Count > 0 && MaxThreads - threads.Count > 0)
                    {
                        // if there's still work, dequeue something
                        queue.TryDequeue(out task); 
                    }
                }
            }
            

            // there isn't a task to do, but there is a stopped thread
            if (task == null && stoppedThread != null) 
            {
                // remove the stopped thread from the list
                lock (threads)
                {
                    if (!threads.Remove(stoppedThread))
                    {
                        throw new SystemException("Could not remove a thread from the list!");
                    }
                    else
                    {
                        Interlocked.Decrement(ref FinishedThreads);
                    }
                }
            }

            // there is a task to do
            else if (task != null)
            {
                Thread newThread = new Thread(new ParameterizedThreadStart(ThreadFunc));

                // thread will not keep an application running after all foreground threads have exited.
                newThread.IsBackground = true;

                lock (threads)
                {
                    if (MaxThreads - threads.Count > 0)
                    {
                        // replace the stopped thread with a new one
                        if (stoppedThread != null) 
                        {
                            threads[threads.IndexOf(stoppedThread)] = newThread;
                        }
                        // add a new thread to the list
                        else
                        {
                            threads.Add(newThread);
                        }
                    }
                    else
                    {
                        // enqueue the task and let other active threads do the work.
                        lock (queue) 
                        {
                            queue.Enqueue(task);
                            task = null;
                        }
                    }
                }

                // let newly added task do its work 
                if (task != null)
                {
                    newThread.Start(task);
                }
            }
        }


        public static void ThreadFunc(Object obj)
        {
            //when a thread is started, it already has a task assigned to it.
            WaitCallback task = obj as WaitCallback;
            if (task == null)
            {
                throw new ArgumentException("ThreadFunc must recieve WaitCallback as a parameter!");
            }
            task.Invoke(null);

            //from now on, I'm dequeueing/invoking tasks from the queue.
            while (true) 
            {
                task = null;

                lock (threads)
                {
                    lock (queue)
                    {
                        //help a non-empty queue to get rid of its load
                        if (queue.Count > 0) 
                        {
                            queue.TryDequeue(out task);
                        }
                    }
                }

                if (task != null)
                {
                    task.Invoke(null);
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

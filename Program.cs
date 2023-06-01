using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Collections.Concurrent;

namespace homework28
{
        
    class Program
   {
        class TaskResult
        {
            public int[] buffer;
            public int begin;
            public int end;
            public int result;
        }

        static void Main(string[] args)
        {
            int count;

            if (args.Length >= 1 && int.TryParse(args[0], out count) && count > 0)
                Run(count);
            else
                Run(10_000_000);            
        }

        static void Run(int count)
        {
            var arr = new int[count];
            var rnd = new Random();
            int result = 0, treadResult = 0, treadResult2 = 0, LINQResult = 0;

            for (int i = 0; i < count; i++)
                arr[i] = rnd.Next(201) - 100;

            var stopwatch = new Stopwatch();

            stopwatch.Start();
            for (int i = 0; i < count; i++)
                result += arr[i];
            stopwatch.Stop();
            Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds}");
            stopwatch.Reset();

            // наивная реализация
            stopwatch.Start();
            var threadCount = Environment.ProcessorCount - 1;
            var chunck = count / threadCount;
            var remainder = count % threadCount;
            var additive = 0;

            if (chunck == 0)
            {
                threadCount = remainder;
                remainder = 0;
                chunck = 1;
            }

            using (var evt = new CountdownEvent(threadCount))
            {

                for (int i = 0; i < threadCount;)
                {
                    var beginChunck = i++ * chunck + additive;
                    if (remainder > 0)
                    {
                        remainder--;
                        additive++;
                    }
                    var endChunck = i * chunck + additive;

                    new Thread(() =>
                    {
                        int subResult = 0;
                        for (int i = beginChunck; i < endChunck; i++)
                            subResult += arr[i];
                        Interlocked.Add(ref treadResult, subResult);

                        evt.Signal();
                    }).Start();
                }
                evt.Wait();
            }
            stopwatch.Stop();
            Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds}");
            stopwatch.Reset();

            //chunckSize
            //к моменту запуска поледнего потока вервые скорее всего уже завершатся
            //желательно балансировать нагрузку
            stopwatch.Start();
            var chunckSize = 2 << 14; //лучше бы побольше, но тогда на 100 000 000 не будет потоков
            var chunckNum = count / chunckSize;
            int realyThreadStarted = 0; ;

            var queue = new ConcurrentQueue<TaskResult>();
            int num = 0;
            var x = new TaskResult();
            for (; num  < count - chunckSize;)
            {                
                x.buffer = arr;
                x.begin = num;
                x.end = num +=  chunckSize;
                x.result = 0;
                queue.Enqueue(x);
                x = new TaskResult();
            }
            
            x.buffer = arr;
            x.begin = num;
            x.end = count;
            x.result = 0;
            queue.Enqueue(x);

            var reduceResult = new int[threadCount];

            using (var evt = new CountdownEvent(threadCount))
            {

                for (int i = 0; i < threadCount; i++)
                {
                    if (!queue.IsEmpty)
                    {
                        var threadNumber = i;
                        new Thread(() =>
                        {
                            Interlocked.Add(ref realyThreadStarted, 1);
                            int result = 0;
                            while (queue.TryDequeue(out TaskResult qResult))
                            {
                                for (int j = qResult.begin; j < qResult.end; j++)
                                    result += qResult.buffer[j];
                            }
                            reduceResult[threadNumber] = result;
                            evt.Signal();
                        }).Start();
                    }
                    else
                        evt.Signal();
                }
                
                evt.Wait();

                for (int i = 0; i < realyThreadStarted; i++)
                    treadResult2 += reduceResult[i];
            }

            stopwatch.Stop();
            Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds}");
            stopwatch.Reset();

            stopwatch.Start();
            LINQResult = arr.AsParallel().Sum();
            stopwatch.Stop();
            Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds}");

            Console.WriteLine($"Serial result {result}, thread result {treadResult}, thread2 result {treadResult2} ({realyThreadStarted}) linq result {LINQResult}");
        }
   }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiTaskLab
{
    public class Sample3
    {
        private const int changeStatusDelay = 10000;

        // 1. 新增變數
        private volatile bool isReceived = false;

        public async Task Start()
        {
            Console.WriteLine($"{nameof(Sample1)} - Enter");

            Delay4();

            Task<int> winningTask = await Task.WhenAny(
                NormalDelay(1000),
                NormalDelay(2000),
                NormalDelay(3000),
                Delay5()
            );

            Console.WriteLine($"{nameof(Sample1)} - Result: {winningTask.Result}");
            Console.WriteLine($"{nameof(Sample1)} - Done");
        }

        async Task<int> NormalDelay(int waitingMS)
        {
            Console.WriteLine($"{nameof(NormalDelay)}({waitingMS}) - Enter");

            await Task.Delay(waitingMS);

            Console.WriteLine($"{nameof(NormalDelay)}({waitingMS}) - Return");
            return 0;
        }


        // 2. 使用 TaskCompletionSource
        Task<int> Delay4()
        {
            Console.WriteLine($"{nameof(Delay4)} - Enter");

            var tcs = new TaskCompletionSource<int>();

            _ = Task.Run(async () =>
            {
                await Task.Delay(changeStatusDelay);

                isReceived = true;
                Console.WriteLine($"{nameof(Delay4)} - Change Status");

                Console.WriteLine($"{nameof(Delay4)} - Return");
                tcs.TrySetResult(0);
            });

            return tcs.Task;
        }

        // 3. 觀察 isReceived，最多等 500 ms
        async Task<int> Delay5()
        {
            Console.WriteLine($"{nameof(Delay5)} - Enter");

            const int checkIntervalMs = 20;
            const int timeoutMs = 5000;
            int waited = 0;

            while (waited < timeoutMs)
            {
                if (isReceived)
                {
                    Console.WriteLine($"{nameof(Delay5)} - Return (isReceived == true)");
                    return 0;
                }

                await Task.Delay(checkIntervalMs);
                waited += checkIntervalMs;
            }

            Console.WriteLine($"{nameof(Delay5)} - Return (timeout)");
            return -1;
        }
    }
}

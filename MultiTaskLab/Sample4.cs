
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiTaskLab
{
    public class Sample4
    {
        private const int changeStatusDelay = 100;
        private const int delay5WaitingMS = 5000;

        // 1. 新增變數（仍可保留做狀態）
        private volatile bool isReceived = false;

        // 🔑 共享的事件來源：Delay4 發事件、Delay5 等事件
        private readonly TaskCompletionSource<int> receiveTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task Start()
        {
            Console.WriteLine($"{nameof(Sample4)} - Enter");

            // 讓事件流程開始（不等待）
            _ = SendMessage();

            Task<int> winningTask = await Task.WhenAny(
                NormalDelay(1000),
                NormalDelay(2000),
                NormalDelay(3000),
                CheckStatus()
            );

            Console.WriteLine($"{nameof(Sample4)} - Result: {winningTask.Result}");
            Console.WriteLine($"{nameof(Sample4)} - Done");
        }

        async Task<int> NormalDelay(int waitingMS)
        {
            Console.WriteLine($"{nameof(NormalDelay)}({waitingMS}) - Enter");

            await Task.Delay(waitingMS);

            Console.WriteLine($"{nameof(NormalDelay)}({waitingMS}) - Return");
            return 0;
        }

        // 2. SendMessage：等待 changeStatusDelay 後，改狀態並發出事件
        async Task<int> SendMessage()
        {
            Console.WriteLine($"{nameof(SendMessage)} - Enter");

            await Task.Delay(changeStatusDelay);

            isReceived = true;
            Console.WriteLine($"{nameof(SendMessage)} - Change Status");

            Console.WriteLine($"{nameof(SendMessage)} - Return");
            receiveTcs.TrySetResult(0);

            return 0;
        }

        // 3. CheckStatus：不輪詢，直接等「事件 or timeout」
        async Task<int> CheckStatus()
        {
            Console.WriteLine($"{nameof(CheckStatus)} - Enter");

            Task completed = await Task.WhenAny(receiveTcs.Task, Task.Delay(delay5WaitingMS));

            if (completed == receiveTcs.Task)
            {
                Console.WriteLine($"{nameof(CheckStatus)} - Return (event received)");
                return 0;
            }

            Console.WriteLine($"{nameof(CheckStatus)} - Return (timeout)");
            return -1;
        }
    }
}

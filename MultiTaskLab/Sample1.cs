using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiTaskLab
{
    public class Sample1
    {
        public async Task Start()
        {
            Console.WriteLine($"{typeof(Sample1).Name} - Enter");

            Task<int> winningTask = await Task.WhenAny(Delay1(), Delay2(), Delay3());
            Console.WriteLine($"{typeof(Sample1).Name} - Result: {winningTask.Result}");

            Console.WriteLine($"{typeof(Sample1).Name} - Done");
        }

        async Task<int> Delay1()
        {
            await Task.Delay(1000);
            return 1;
        }
        async Task<int> Delay2()
        {
            await Task.Delay(2000);
            return 2;
        }
        async Task<int> Delay3()
        {
            await Task.Delay(3000);
            return 3;
        }
    }
}

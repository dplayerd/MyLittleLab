using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiTaskLab
{
    public class Sample2
    {
        public async Task Start()
        {
            string name = typeof(Sample2).Name;


            Console.WriteLine($"{name} - Enter");

            await Task.WhenAll(Delay1(), Delay2(), Delay3());

            Console.WriteLine($"{name} - Done");
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

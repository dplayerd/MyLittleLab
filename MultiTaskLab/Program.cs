namespace MultiTaskLab
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Sample4 obj = new();

            await obj.Start();

            Console.WriteLine("Hello, World!");
        }
    }
}

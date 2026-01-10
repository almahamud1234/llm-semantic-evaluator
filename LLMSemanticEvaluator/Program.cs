namespace LLMSemanticEvaluator
{
    public class Program
    {
        
        // static async Task Main(string[] args)
        static void Main(string[] args)
        {
            Console.WriteLine("ML Project Started");

            // Example: call your core logic here
            var result = Calculator.Add(2, 3);
            Console.WriteLine($"Result: {result}");
        }
    }

    public static class Calculator
    {
        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}

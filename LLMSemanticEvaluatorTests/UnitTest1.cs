using LLMSemanticEvaluator;
using Xunit;


namespace LLMSemanticEvaluatorTests
{
    public class CalculatorTests
    {
        [Fact]
        public void Add_ReturnsCorrectSum()
        {
            // Arrange
            int a = 2;
            int b = 3;

            // Act
            int result = Calculator.Add(a, b);

            // Assert
            Assert.Equal(5, result);
        }
    }
}
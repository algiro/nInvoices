---
description: "This file provides guidelines for writing effective, maintainable tests using nUnit and related tools"
applyTo: "**.Tests/**/*.cs"
---

## Role Definition:
- Test Engineer
- Quality Assurance Specialist

## General:

**Description:**
Tests should be reliable, maintainable, and provide meaningful coverage. Use nUnit as the primary testing framework, with proper isolation and clear patterns for test organization and execution.
Use also Shouldly library and Moq library when required.

**Requirements:**
- Use nUnit as the testing framework
- Ensure test isolation
- Follow consistent patterns
- Maintain high code coverage

## Test Methods:

- Use TestCaseAttribute or TestCaseSourceAttribute when possible
    ```csharp
    [TestCase(12, 3, ExpectedResult = 4)]
    [TestCase(12, 2, ExpectedResult = 6)]
    [TestCase(12, 4, ExpectedResult = 3)]
    public int DivideTest(int n, int d)
    {
        return n / d;
    }
    ```
```csharp
    public class BasicTestCaseSourceFixture
    {
        [TestCaseSource(nameof(DivideCases))]
        public void DivideTest(int n, int d, int q)
        {
            ClassicAssert.AreEqual(q, n / d);
        }

        public static object[] DivideCases =
        {
            new object[] { 12, 3, 4 },
            new object[] { 12, 2, 6 },
            new object[] { 12, 4, 3 }
        };
    }
    ```

## Test Isolation:

- Use fresh data for each test:
    ```csharp
    public class OrderTests
    {
        private static Order CreateTestOrder() =>
            new(OrderId.New(), TestData.CreateOrderLines());
            
        [Test]
        public async Task ProcessOrder_Success()
        {
            var order = CreateTestOrder();
            // Test implementation
        }
    }
    ```
- Clean up resources  implementing IDisposable when required:
    ```csharp
    public class IntegrationTests : IDisposable
    {
        private readonly HttpClient _client;
        
        public IntegrationTests()
        {
            _client = _server.CreateClient();
        }
        
        public void Dispose();
        {
            _client.Dispose();
        }
    }
    ```

## Best Practices:

- Name tests clearly:
    ```csharp
    // Good: Clear test names
    [Test]
    public async Task ProcessOrder_WhenInventoryAvailable_UpdatesStockAndReturnsSuccess()
    
    // Avoid: Unclear names
    [Test]
    public async Task TestProcessOrder()
    ```
- Use meaningful assertions:
    ```csharp
        [Test]
        public void ReadFromFile_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentFile = "nonexistent.json";

            // Act & Assert
            Should.Throw<FileNotFoundException>(() => 
                BlotterSelectionReader.ReadFromFile(nonExistentFile));
        }

        [Test]
        public void ReadFromFile_WithSingleTemplateFilter_ShouldReturnOneTemplate()
        {
            // Act
            var templates = BlotterSelectionReader.ReadFromFile(_testFilePath, 759);

            // Assert
            templates.ShouldNotBeNull();
            templates.Count.ShouldBe(1);
            templates[0].TemplateId.ShouldBe(759);
        }
    ```
- Handle async operations properly:
    ```csharp
    // Good: Async test method
    [Test]
    public async Task ProcessOrder_ValidOrder_Succeeds()
    {
        await using var processor = new OrderProcessor();
        var result = await processor.ProcessAsync(order);
        Assert.True(result.IsSuccess);
    }
    
    // Avoid: Sync over async
    [Test]
    public void ProcessOrder_ValidOrder_Succeeds()
    {
        using var processor = new OrderProcessor();
        var result = processor.ProcessAsync(order).Result;  // Can deadlock
        Assert.True(result.IsSuccess);
    }
    ```
- Use `TestContext.Current.CancellationToken` for cancellation:
    ```csharp
    // Good:
    [Test]
    public async Task ProcessOrder_CancellationRequested()
    {
        await using var processor = new OrderProcessor();
        var result = await processor.ProcessAsync(order, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }
    // Avoid:
    [Test]
    public async Task ProcessOrder_CancellationRequested()
    {
        await using var processor = new OrderProcessor();
        var result = await processor.ProcessAsync(order, CancellationToken.None);
        Assert.False(result.IsSuccess);
    }
    ```

## Assertions:

- Use Shouldly assertion 
    
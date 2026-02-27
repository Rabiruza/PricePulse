# HTTP Test Server Setup for E2E Tests

## Overview

This document describes the HTTP test server setup for running PricePulse E2E tests locally and in CI/CD.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    PriceTrackerE2ETests                     │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │ TestServer   │  │   Playwright │  │  PriceTracker   │   │
│  │   Fixture    │  │   Browser    │  │     (CLI)       │   │
│  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘   │
│         │                 │                    │            │
│         ▼                 ▼                    ▼            │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │  HttpListener│  │  Chromium    │  │  WebPriceExtractor│ │
│  │  (localhost) │  │  (Headless)  │  │  (Playwright)   │   │
│  └──────┬───────┘  └──────────────┘  └────────┬────────┘   │
│         │                                      │            │
│         └──────────────────┬───────────────────┘            │
│                            │                                │
│                            ▼                                │
│                 ┌─────────────────────┐                     │
│                 │  HTML Test Files    │                     │
│                 │  - test-page-1.html │                     │
│                 │  - dynamic-price... │                     │
│                 │  - sale-item.html   │                     │
│                 └─────────────────────┘                     │
└─────────────────────────────────────────────────────────────┘
```

## Components

### 1. TestServer (`IntegrationTests/TestServer/TestServer.cs`)

A lightweight HTTP server using `HttpListener` that serves static HTML files for testing.

**Features:**
- Automatically finds available port
- Serves HTML files from `IntegrationTests/TestServer/` directory
- Path traversal protection (security)
- Proper request/response handling
- Async disposal support

### 2. TestServerFixture (`IntegrationTests/TestServer/TestServerFixture.cs`)

Xunit fixture that manages the test server lifecycle.

**Usage:**
```csharp
public class MyE2ETests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;
    
    public MyE2ETests(TestServerFixture fixture)
    {
        _fixture = fixture;
        // Access via: _fixture.BaseAddress
    }
}
```

### 3. HTML Test Files (`IntegrationTests/TestServer/*.html`)

Static HTML pages that simulate real e-commerce product pages:

| File | Purpose | Price Selector |
|------|---------|----------------|
| `test-page-1.html` | Basic static price page | `.price` ($799.99) |
| `dynamic-price-page.html` | JavaScript-loaded price | `#dynamic-price` ($599.00 after 1s) |
| `sale-item.html` | Dynamic sale price | `#sale-price` ($399.99 after 1.5s) |

## Running Tests

### Locally

```bash
# Run all E2E tests
dotnet test --filter "FullyQualifiedName~PriceTrackerE2ETests"

# Run specific test
dotnet test --filter "FullyQualifiedName~MCP_ExtractsPrice_FromTestPage"

# Run with verbose output
dotnet test --filter "FullyQualifiedName~PriceTrackerE2ETests" --logger "console;verbosity=detailed"
```

### In CI/CD (GitHub Actions)

```yaml
- name: Run E2E Tests
  run: dotnet test PricePulse.Tests/PricePulse.Tests.csproj \
        --filter "Category=E2E" \
        --logger "console;verbosity=detailed"
```

## Test Examples

### Basic Price Extraction Test

```csharp
[Fact]
public async Task ExtractsPrice_FromTestPage()
{
    // Arrange
    var context = await _browser.NewContextAsync();
    var page = await context.NewPageAsync();
    
    // Act
    await page.GotoAsync($"{_fixture.BaseAddress}/test-page-1.html");
    var element = await page.WaitForSelectorAsync(".price");
    var priceText = await element.TextContentAsync();
    
    // Assert
    Assert.Contains("$799.99", priceText);
}
```

### Dynamic Content Test

```csharp
[Fact]
public async Task HandlesDynamicPriceLoading()
{
    var page = await _browser.NewPageAsync();
    
    // Navigate to page with JavaScript-loaded price
    await page.GotoAsync($"{_fixture.BaseAddress}/dynamic-price-page.html");
    
    // Wait for dynamic content
    var element = await page.WaitForSelectorAsync("#dynamic-price");
    await element.WaitForElementStateAsync("stable");
    
    var priceText = await element.TextContentAsync();
    Assert.Contains("$599.00", priceText);
}
```

### Full Workflow Test

```csharp
[Fact]
public async Task PriceTracker_FullWorkflow()
{
    // Arrange - Mock services
    var mockStorage = new Mock<IPriceStorage>();
    mockStorage.Setup(x => x.GetLastPriceAsync()).ReturnsAsync(999.99m);
    
    var mockNotification = new Mock<INotificationService>();
    
    // Act - Run tracker against test server
    var tracker = new PriceTracker(/* ... */);
    await tracker.RunAsync(new ProductConfig
    {
        Id = "test",
        DisplayName = "Test Product",
        Url = $"{_fixture.BaseAddress}/sale-item.html",
        CssSelector = "#sale-price"
    });
    
    // Assert - Notification sent for price drop
    mockNotification.Verify(x => x.SendAsync(It.Contains("SALE")), Times.Once);
}
```

## Debugging

### Enable Verbose Logging

The test uses `TestLogger` which outputs to Xunit's `ITestOutputHelper`:

```csharp
[Fact]
public async Task TestWithLogging()
{
    var logger = new TestLogger<MyClass>(_output);
    logger.LogInformation("Debug info: {Value}", someValue);
    // Output visible in test runner
}
```

### Screenshots

Tests automatically save screenshots on success/failure:

```
PricePulse.Tests/bin/Debug/net8.0/Screenshots/
├── Success/
│   └── MCP_ExtractsPrice_FromTestPage_20260227_150708.png
└── Error/
    └── FailedTest_20260227_150800.png
```

### Common Issues

**Issue: "Address already in use"**
- Solution: The `TestServerFixture` automatically finds an available port

**Issue: "File not found" for HTML files**
- Solution: Ensure HTML files are copied to output:
  ```xml
  <Content Include="IntegrationTests\TestServer\*.html">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  ```

**Issue: Timeout waiting for selector**
- Solution: Increase timeout or check if JavaScript needs time to execute:
  ```csharp
  await page.WaitForSelectorAsync(".price", new() { Timeout = 10000 });
  ```

## Port Allocation

The test server uses dynamic port allocation to avoid conflicts:

```csharp
private static int FindAvailablePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}
```

This ensures multiple test runs can execute in parallel without port conflicts.

## Security

The `TestServer` includes path traversal protection:

```csharp
// Prevent directory traversal attacks
if (!fullFilePath.StartsWith(fullContentRoot, StringComparison.OrdinalIgnoreCase))
{
    context.Response.StatusCode = 403;
    return;
}
```

## Performance

Typical test execution times:
- Server startup: < 100ms
- Page load (local): < 500ms
- Price extraction: < 1s
- Total per test: 1-3 seconds

## Next Steps

1. Add more test scenarios (cart, checkout flows if applicable)
2. Create test pages with edge cases (missing prices, malformed HTML)
3. Add visual regression testing with Playwright screenshots
4. Integrate with CI/CD pipeline
5. Add performance benchmarks

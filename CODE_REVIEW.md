# PricePulse - Code Review & Architectural Analysis

## Executive Summary

**Overall Assessment**: The project demonstrates good architectural principles with clean separation of concerns, but has several critical issues that need addressing before production deployment.

**Strengths**:
- ✅ Clean dependency injection pattern
- ✅ Well-defined interfaces
- ✅ Good test coverage with mocking
- ✅ Modern .NET 8 stack

**Critical Issues**:
- 🔴 Resource leaks (HttpClient, Playwright)
- 🔴 Silent failures
- 🔴 Hardcoded values
- 🔴 Missing error handling
- 🔴 Security concerns

---

## 🔍 CODE REVIEW

### 1. **CRITICAL: Resource Management Issues**

#### Issue 1.1: HttpClient Disposal
**Location**: `PrometheusMonitoringService.cs`, `TelegramNotificationService.cs`

**Problem**:
```csharp
// PrometheusMonitoringService.cs
public PrometheusMonitoringService()
{
    _httpClient = new HttpClient(); // ❌ Never disposed
}

// TelegramNotificationService.cs
using var client = new HttpClient(); // ✅ Good, but inconsistent pattern
```

**Impact**: 
- `PrometheusMonitoringService` creates an `HttpClient` that is never disposed, leading to socket exhaustion
- Inconsistent patterns across services
- Memory leaks in long-running processes

**Recommendation**:
- Use `IHttpClientFactory` (best practice)
- Or implement `IDisposable` and dispose `HttpClient` properly
- Or use `using` pattern consistently

#### Issue 1.2: Playwright Resource Management
**Location**: `WebPriceExtractor.cs:13-14`

**Problem**:
```csharp
using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(...);
```

**Analysis**: ✅ Actually correct! `using var` ensures disposal. However, consider:
- Playwright instances are expensive to create
- Creating a new browser for every price check is inefficient
- Should be reused or pooled for better performance

**Recommendation**: Consider singleton pattern or dependency injection for Playwright instance.

---

### 2. **CRITICAL: Silent Failures**

#### Issue 2.1: Swallowed Exceptions
**Location**: Multiple files

**Problems**:
```csharp
// PriceStorage.cs:16
catch { return 0; } // ❌ Silent failure - no logging

// PrometheusMonitoringService.cs:29-32
catch { 
    // Ignore monitoring issues // ❌ Silent failure
}

// TelegramNotificationService.cs:12
if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId)) return; // ❌ Silent failure
```

**Impact**:
- Failures go unnoticed
- Difficult to debug production issues
- No observability into system health

**Recommendation**:
- Use structured logging (e.g., `ILogger<T>`)
- At minimum, log exceptions with context
- Consider returning `Result<T>` pattern or exceptions for critical failures

#### Issue 2.2: Price Extraction Failure Handling
**Location**: `WebPriceExtractor.cs:41-48`

**Problem**:
```csharp
catch (TimeoutException)
{
    Console.WriteLine("[Scraper] Timeout..."); // ❌ Only console output
}
catch (Exception ex)
{
    Console.WriteLine($"[Scraper] Error: {ex.Message}"); // ❌ Only console output
}
return 0m; // ❌ Indistinguishable from "price is zero"
```

**Impact**:
- Cannot distinguish between "price not found" and "actual price is $0"
- No retry logic
- No alerting on persistent failures

**Recommendation**:
- Return `Result<decimal>` or `Option<decimal>` pattern
- Or throw custom exceptions (`PriceExtractionException`)
- Add retry logic with exponential backoff

---

### 3. **HIGH: Error Handling & Validation**

#### Issue 3.1: Missing Input Validation
**Location**: `PriceTracker.cs:24-26`

**Problem**:
```csharp
public async Task RunAsync(string url, string modelName)
{
    decimal currentPrice = await _priceProvider.GetPriceAsync(url);
    if (currentPrice <= 0) return; // ❌ Silent failure, no logging
```

**Issues**:
- No validation of `url` (could be null/empty/invalid)
- No validation of `modelName`
- Silent return on failure

**Recommendation**:
```csharp
public async Task RunAsync(string url, string modelName)
{
    if (string.IsNullOrWhiteSpace(url))
        throw new ArgumentException("URL cannot be null or empty", nameof(url));
    
    if (string.IsNullOrWhiteSpace(modelName))
        throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
    
    decimal currentPrice = await _priceProvider.GetPriceAsync(url);
    if (currentPrice <= 0)
    {
        _logger.LogWarning("Failed to retrieve price for {ModelName} from {Url}", modelName, url);
        return;
    }
```

#### Issue 3.2: No Exception Handling in PriceTracker
**Location**: `PriceTracker.cs`

**Problem**: If any dependency throws an exception, the entire operation fails with no recovery.

**Recommendation**: Add try-catch blocks with appropriate error handling and logging.

---

### 4. **HIGH: Hardcoded Values & Configuration**

#### Issue 4.1: Hardcoded File Path
**Location**: `PriceStorage.cs:8`

**Problem**:
```csharp
private const string HistoryFile = "price_history.json"; // ❌ Hardcoded
```

**Issues**:
- Cannot configure storage location
- File location relative to working directory (unpredictable)
- Cannot use different storage for different products

**Recommendation**: Use `IOptions<StorageOptions>` or constructor parameter.

#### Issue 4.2: Hardcoded Prometheus Endpoint
**Location**: `PrometheusMonitoringService.cs:24`

**Problem**:
```csharp
var response = await _httpClient.PostAsync("http://localhost:9091/metrics/job/price_pulse_job", content);
// ❌ Hardcoded URL, hardcoded job name
```

**Recommendation**: Move to configuration (appsettings.json, environment variables).

#### Issue 4.3: Hardcoded Selector
**Location**: `WebPriceExtractor.cs:26`

**Problem**:
```csharp
string selector = "span[data-pricing-product='iphone-17']"; // ❌ Hardcoded for iPhone only
```

**Issues**:
- Not reusable for other products
- Breaks if Apple changes HTML structure
- Should be configurable per product

**Recommendation**: Pass selector as parameter or configuration.

---

### 5. **MEDIUM: Code Quality Issues**

#### Issue 5.1: Console.WriteLine in Production Code
**Location**: Multiple files

**Problem**: Direct `Console.WriteLine` calls scattered throughout:
- `WebPriceExtractor.cs:33, 43, 47`
- `PrometheusMonitoringService.cs:27`
- `Program.cs:5`

**Impact**: 
- Cannot control log levels
- Cannot redirect to structured logging systems
- Difficult to filter/search logs

**Recommendation**: Use `ILogger<T>` throughout.

#### Issue 5.2: Magic Numbers
**Location**: `WebPriceExtractor.cs:27`

**Problem**:
```csharp
await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
// ❌ Magic number: 10000ms
```

**Recommendation**: Extract to named constant or configuration.

#### Issue 5.3: Inconsistent Naming
**Location**: `PriceTrackerTests.cs` (class name doesn't match file name)

**Problem**: File is `PriceTrackerTests.cs` but class is `PriceProviderTests`.

**Recommendation**: Rename class to match file or vice versa.

---

### 6. **MEDIUM: Security Concerns**

#### Issue 6.1: URL Injection Risk
**Location**: `WebPriceExtractor.cs:22`

**Problem**: No validation that URL is safe before navigating.

**Recommendation**: Validate URL scheme (must be http/https) and potentially whitelist domains.

#### Issue 6.2: Telegram Token Exposure
**Location**: `TelegramNotificationService.cs:9`

**Problem**: Token read from environment variable (✅ good), but no validation that it's set.

**Recommendation**: Validate at startup, fail fast if missing.

---

### 7. **LOW: Code Style & Best Practices**

#### Issue 7.1: Unused Dependencies
**Location**: `PricePulse.Core.csproj:12-14`

**Problem**:
```xml
<PackageReference Include="prometheus-net" Version="8.2.1" />
<PackageReference Include="prometheus-net.AspNetCore" Version="8.2.1" />
<PackageReference Include="prometheus-net.DotNetRuntime" Version="4.4.1" />
```

**Analysis**: These packages are not used anywhere. The project uses Pushgateway directly via HTTP.

**Recommendation**: Remove unused packages or document why they're needed.

#### Issue 7.2: Missing XML Documentation
**Location**: Most public methods lack XML docs

**Recommendation**: Add XML documentation comments for public APIs.

#### Issue 7.3: Price Parsing Logic
**Location**: `WebPriceExtractor.cs:34-37`

**Problem**:
```csharp
var match = Regex.Match(priceText, @"\d+");
if (match.Success)
{
    return decimal.Parse(match.Value, CultureInfo.InvariantCulture);
}
```

**Issues**:
- Only extracts first number (e.g., "From $799" → 799 ✅, but "Save $100 on $799" → 100 ❌)
- Doesn't handle decimal prices
- Doesn't handle currency symbols properly

**Recommendation**: Improve regex to handle common price formats, or use a more robust parsing library.

---

## 🏗️ ARCHITECTURAL REVIEW

### 1. **Architecture Pattern: Layered Architecture ✅**

**Current Structure**:
```
Program.cs (Composition Root)
    ↓
PriceTracker (Domain Service)
    ↓
Interfaces (Abstractions)
    ↓
Services (Implementations)
```

**Assessment**: ✅ **Good** - Clean separation of concerns, dependency inversion principle followed.

**Strengths**:
- Clear boundaries between layers
- Dependencies point inward (toward abstractions)
- Easy to test (interfaces enable mocking)

**Recommendations**:
- Consider adding a `Domain` layer for business entities
- Consider adding an `Infrastructure` layer for external dependencies

---

### 2. **Dependency Injection: Manual DI ✅**

**Current Approach**: Manual constructor injection in `Program.cs`

**Assessment**: ✅ **Adequate for small projects**, but has limitations.

**Strengths**:
- Simple and explicit
- No external DI container dependency
- Easy to understand

**Limitations**:
- Doesn't scale well (imagine 20+ services)
- No lifetime management (singleton vs transient)
- No automatic disposal
- Harder to test (can't easily swap implementations)

**Recommendation for Growth**:
- Consider `Microsoft.Extensions.DependencyInjection`:
  ```csharp
  var services = new ServiceCollection();
  services.AddSingleton<IHttpClientFactory, HttpClientFactory>();
  services.AddTransient<IPriceProvider, WebPriceExtractor>();
  services.AddSingleton<IPriceStorage, PriceStorage>();
  services.AddTransient<INotificationService, TelegramNotificationService>();
  services.AddSingleton<IMonitoringService, PrometheusMonitoringService>();
  services.AddTransient<PriceTracker>();
  
  var provider = services.BuildServiceProvider();
  var tracker = provider.GetRequiredService<PriceTracker>();
  ```

---

### 3. **Scalability Concerns**

#### Issue 3.1: Single Product Limitation
**Current**: Hardcoded to track one product (iPhone 17)

**Problem**: Architecture doesn't support multiple products easily.

**Recommendation**: 
- Add `Product` entity/configuration
- Make `PriceTracker` accept product configuration
- Support multiple products in a single run or separate instances

#### Issue 3.2: Sequential Processing
**Current**: Processes one product at a time

**Problem**: If tracking 100 products, takes 100x longer.

**Recommendation**: 
- Add parallel processing with `Task.WhenAll`
- Consider message queue (RabbitMQ, Azure Service Bus) for scale-out

#### Issue 3.3: Storage Limitations
**Current**: Single JSON file stores one price

**Problem**: 
- Cannot track price history over time
- Cannot track multiple products
- File-based storage doesn't scale

**Recommendation**:
- Use proper database (SQLite for simple, PostgreSQL for production)
- Store price history with timestamps
- Support querying price trends

---

### 4. **Observability & Monitoring**

#### Current State:
- ✅ Prometheus metrics (via Pushgateway)
- ❌ No structured logging
- ❌ No distributed tracing
- ❌ No health checks
- ❌ No metrics for operation duration/failure rates

#### Recommendations:
1. **Add Structured Logging**:
   - Use `Serilog` or `Microsoft.Extensions.Logging`
   - Log all operations with context (product, price, duration)
   - Use log levels appropriately

2. **Add Application Metrics**:
   - Track: price check duration, success/failure rates, notification send success
   - Use Prometheus client library properly (not just Pushgateway)

3. **Add Health Checks**:
   - Health endpoint for monitoring systems
   - Check: storage accessible, Telegram API reachable, Prometheus reachable

---

### 5. **Testability: Good ✅**

**Current State**:
- ✅ Interfaces enable mocking
- ✅ Good unit test example with Moq
- ✅ Tests isolate business logic

**Issues**:
- ❌ Integration test hits real Microsoft website (flaky)
- ❌ No tests for error scenarios
- ❌ No tests for edge cases (null inputs, empty strings)

**Recommendations**:
- Add more test coverage:
  - Price increase scenario
  - First run scenario
  - Storage failure scenario
  - Notification failure scenario
- Mock external dependencies in integration tests
- Add property-based tests for price parsing

---

### 6. **Configuration Management**

#### Current State:
- ❌ Hardcoded values everywhere
- ✅ Environment variables for secrets (Telegram)

#### Recommendations:
1. **Add Configuration System**:
   ```csharp
   // appsettings.json
   {
     "Storage": {
       "HistoryFile": "price_history.json"
     },
     "Prometheus": {
       "PushGatewayUrl": "http://localhost:9091",
       "JobName": "price_pulse_job"
     },
     "Products": [
       {
         "Name": "iPhone 17",
         "Url": "https://www.apple.com/iphone-17/",
         "Selector": "span[data-pricing-product='iphone-17']"
       }
     ]
   }
   ```

2. **Use Options Pattern**:
   ```csharp
   services.Configure<StorageOptions>(configuration.GetSection("Storage"));
   services.Configure<PrometheusOptions>(configuration.GetSection("Prometheus"));
   ```

---

### 7. **Error Recovery & Resilience**

#### Current State:
- ❌ No retry logic
- ❌ No circuit breaker
- ❌ Fails completely on any error

#### Recommendations:
1. **Add Retry Logic**:
   - Use `Polly` library for retry policies
   - Retry transient failures (network, timeouts)
   - Exponential backoff

2. **Add Circuit Breaker**:
   - Prevent cascading failures
   - Stop hammering failing services

3. **Graceful Degradation**:
   - Continue tracking even if notifications fail
   - Continue tracking even if monitoring fails
   - Log all failures for later analysis

---

### 8. **Deployment & Operations**

#### Current State:
- ✅ Docker Compose for monitoring stack
- ✅ GitHub Actions for CI/CD
- ❌ No application containerization
- ❌ No health checks
- ❌ No graceful shutdown

#### Recommendations:
1. **Containerize Application**:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   COPY bin/Release/net8.0/ /app/
   WORKDIR /app
   ENTRYPOINT ["dotnet", "PricePulse.Core.dll"]
   ```

2. **Add Graceful Shutdown**:
   - Handle `CancellationToken`
   - Complete in-flight operations before exit

3. **Add Health Checks**:
   - Implement `IHealthCheck` interface
   - Expose health endpoint

---

## 📊 Priority Matrix

### 🔴 Critical (Fix Immediately)
1. HttpClient disposal in `PrometheusMonitoringService`
2. Silent exception swallowing
3. Input validation in `PriceTracker`

### 🟠 High (Fix Soon)
1. Structured logging
2. Configuration management
3. Error handling improvements
4. Resource pooling (Playwright)

### 🟡 Medium (Plan for Next Sprint)
1. Remove unused dependencies
2. Improve price parsing logic
3. Add more test coverage
4. Add XML documentation

### 🟢 Low (Nice to Have)
1. Add distributed tracing
2. Add circuit breaker pattern
3. Support multiple products
4. Database migration

---

## 🎯 Recommended Next Steps

1. **Immediate Actions**:
   - Fix HttpClient disposal
   - Add structured logging
   - Add input validation

2. **Short-term (1-2 weeks)**:
   - Implement configuration system
   - Add comprehensive error handling
   - Improve test coverage

3. **Medium-term (1 month)**:
   - Add DI container
   - Support multiple products
   - Migrate to database storage

4. **Long-term (3+ months)**:
   - Add distributed architecture support
   - Implement retry/circuit breaker
   - Add comprehensive observability

---

## 📝 Summary

**Code Quality**: 6/10
- Good structure and patterns
- Critical resource management issues
- Needs better error handling

**Architecture**: 7/10
- Clean separation of concerns
- Good testability
- Needs scalability improvements
- Needs better observability

**Production Readiness**: 4/10
- Not ready for production as-is
- Critical issues must be fixed
- Needs operational improvements

**Recommendation**: Address critical issues before production deployment. The foundation is solid, but needs hardening for reliability and observability.

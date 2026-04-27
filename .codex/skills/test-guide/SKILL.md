---
name: test-guide
description: >
  Guides writing unit tests for a C# .NET game server using xUnit and NSubstitute.
  Use whenever writing or fixing tests — including "write a test", "add unit tests",
  "test this method", or "why is this test failing".
  Consistently applies the Given-When-Then structure and Mock patterns.
---

## Test Framework

- **xUnit**: Test runner
- **NSubstitute**: Mocking (do not use Moq)
- **FluentAssertions**: Assertions

---

## Test Structure — Given-When-Then

```csharp
public class GachaSystemTests
{
    private readonly IGachaService _gachaService;
    private readonly GachaSystem _sut; // System Under Test

    public GachaSystemTests()
    {
        _gachaService = Substitute.For<IGachaService>();
        _sut = new GachaSystem(_gachaService);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsGachaResult()
    {
        // Given
        var request = new GachaRequest { PlayerId = 1L };
        var dto = new GachaResultDto
        {
            ItemId = "sword_epic_001",
            Rarity = Rarity.EPIC,
            PityCount = 42
        };
        _gachaService.PullAsync(1L, Arg.Any<CancellationToken>())
                     .Returns(dto);

        // When
        var result = await _sut.ExecuteAsync(request);

        // Then
        result.ItemId.Should().Be("sword_epic_001");
        result.Rarity.Should().Be(Rarity.EPIC);
        await _gachaService.Received(1).PullAsync(1L, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPlayerId_ThrowsGameException()
    {
        // Given
        var request = new GachaRequest { PlayerId = -1L };

        // When
        var act = () => _sut.ExecuteAsync(request);

        // Then
        await act.Should().ThrowAsync<GameException>()
                 .WithMessage("Invalid player ID.");
    }
}
```

---

## NSubstitute Patterns

```csharp
// Create a mock
var service = Substitute.For<IGachaService>();

// Set return value
service.PullAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
       .Returns(new GachaResultDto { ... });

// Throw an exception
service.PullAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
       .ThrowsAsync(new GameException("Error"));

// Void / Task method
service.RecordAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
       .Returns(Task.CompletedTask);

// Verify call count
await service.Received(1).PullAsync(1L, Arg.Any<CancellationToken>());
await service.DidNotReceive().PullAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
```

---

## Test Naming Convention

```
{Method}_{Condition}_{ExpectedResult}
```

Examples:
- `ExecuteAsync_ValidRequest_ReturnsGachaResult`
- `ExecuteAsync_InvalidPlayerId_ThrowsGameException`
- `SelectByWeight_AllZeroWeights_ReturnsFallback`

---

## Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test GameServer.Tests/

# Specific class
dotnet test --filter "FullyQualifiedName~GachaSystemTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Rules

- No real network or DB calls — replace everything with mocks
- No `Thread.Sleep` / `Task.Delay` — use `CancellationToken` or virtual time
- Max 5 assertions (Then) per test — split if you need more
- When testing server-authoritative logic, always use values the client could plausibly manipulate as input

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using zombie_survival_3Dgame_Server.Common;

namespace zombie_survival_3Dgame_Server.Tests.Common;

public sealed class PersistenceConflictExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ConcurrencyException_ReturnsConflict()
    {
        // Given
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddProblemDetails();
        using var services = serviceCollection.BuildServiceProvider();
        var context = new DefaultHttpContext();
        context.RequestServices = services;
        context.Response.Body = new MemoryStream();
        var handler = new PersistenceConflictExceptionHandler(
            NullLogger<PersistenceConflictExceptionHandler>.Instance);

        // When
        var handled = await handler.TryHandleAsync(
            context,
            new DbUpdateConcurrencyException(),
            cancellationTokenSource.Token);

        // Then
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}

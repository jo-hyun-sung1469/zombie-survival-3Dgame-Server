using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Tests.Data;

public sealed class PlayerSaveConcurrencyTests
{
    [Fact]
    public void Model_PlayerSaveVersion_IsConcurrencyToken()
    {
        // Given
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var context = new GameDbContext(options);

        // When
        var versionProperty = context.Model
            .FindEntityType(typeof(PlayerSaveData))!
            .FindProperty(nameof(PlayerSaveData.Version));

        // Then
        versionProperty.Should().NotBeNull();
        versionProperty!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void MarkChanged_PlayerSave_IncrementsVersion()
    {
        // Given
        var saveData = new PlayerSaveData();

        // When
        saveData.MarkChanged(DateTime.UtcNow);

        // Then
        saveData.Version.Should().Be(1);
    }
}

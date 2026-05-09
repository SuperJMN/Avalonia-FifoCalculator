using System.Security.Cryptography;
using FIFOCalculator.Desktop.Sync;
using FIFOCalculator.Sync;

namespace TestProject1;

public sealed class ZafiroSyncFifoSyncIdentityProviderTests
{
    [Fact]
    public void Create_ShouldReturnEncryptedFifoCalculatorIdentity()
    {
        var sut = new ZafiroSyncFifoSyncIdentityProvider();

        var created = sut.Create("correct-password");
        var imported = sut.Import("correct-password", created.Value.ProtectedExport);

        created.IsSuccess.Should().BeTrue();
        imported.IsSuccess.Should().BeTrue();
        imported.Value.AppId.Should().Be(FifoSyncDefaults.AppId);
        imported.Value.ProtectedExport.Should().Equal(created.Value.ProtectedExport);
    }

    [Fact]
    public void Import_WhenPasswordIsWrong_ShouldFail()
    {
        var sut = new ZafiroSyncFifoSyncIdentityProvider();
        var created = sut.Create("correct-password");

        var imported = sut.Import("wrong-password", created.Value.ProtectedExport);

        imported.IsFailure.Should().BeTrue();
        imported.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Import_WhenIdentityUsesAnotherAppId_ShouldKeepOriginalAppIdForCoordinatorValidation()
    {
        var pokemon = Zafiro.Sync.Client.AppIdentity.Create("pokemon", "Pokemon");
        var export = pokemon.Export("password");
        var sut = new ZafiroSyncFifoSyncIdentityProvider();

        var imported = sut.Import("password", export);

        imported.IsSuccess.Should().BeTrue();
        imported.Value.AppId.Should().Be("pokemon");
    }
}

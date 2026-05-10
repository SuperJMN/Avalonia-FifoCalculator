using System.Text;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using FIFOCalculator.Sync;
using Zafiro.DivineBytes;
using Zafiro.UserStorage;

namespace TestProject1;

public sealed class FifoSyncServiceTests
{
    [Fact]
    public async Task Initialize_WhenNoStoredIdentity_ShouldMarkSyncReadyWithoutIdentity()
    {
        var storage = new InMemoryUserStorage();
        var remote = new FakeRemoteCatalogClient();
        var sut = CreateSut(storage, remote);

        sut.Status.IsReady.Should().BeFalse();

        var result = await sut.Initialize();

        result.IsSuccess.Should().BeTrue();
        sut.Status.IsReady.Should().BeTrue();
        sut.Status.HasIdentity.Should().BeFalse();
        sut.Status.Message.Should().Be("Sync is not configured.");
    }

    [Fact]
    public async Task CreateIdentity_WhenRemoteIsEmpty_ShouldStoreIdentityAndUploadLocalCatalog()
    {
        var catalog = Catalog(1, 100m);
        var storage = new InMemoryUserStorage();
        var remote = new FakeRemoteCatalogClient();
        var sut = CreateSut(storage, remote);

        var result = await sut.CreateIdentity("correct-password", catalog);

        result.IsSuccess.Should().BeTrue();
        (await storage.Exists(FifoSyncDefaults.IdentityKey)).Value.Should().BeTrue();
        remote.Saves.Should().ContainSingle()
            .Which.BaseRevision.Should().BeNull();
        sut.Status.IsUnlocked.Should().BeTrue();
        sut.Status.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task ImportIdentity_WhenPasswordIsWrong_ShouldFailWithoutReplacingStoredIdentity()
    {
        var storage = new InMemoryUserStorage();
        var identityProvider = new FakeIdentityProvider();
        var remote = new FakeRemoteCatalogClient();
        var sut = CreateSut(storage, remote, identityProvider);
        await sut.CreateIdentity("correct-password", Catalog(1, 100m));
        var before = await LoadStoredIdentity(storage);

        identityProvider.ImportShouldFail = true;
        var result = await sut.ImportIdentity(Encoding.UTF8.GetBytes("replacement"), "wrong-password", Catalog(2, 200m));

        result.IsFailure.Should().BeTrue();
        var after = await LoadStoredIdentity(storage);
        after.Should().Equal(before);
    }

    [Fact]
    public async Task ImportIdentity_WhenIdentityBelongsToAnotherApp_ShouldFailWithoutStoringIt()
    {
        var storage = new InMemoryUserStorage();
        var identityProvider = new FakeIdentityProvider { ImportedAppId = "pokemon" };
        var remote = new FakeRemoteCatalogClient();
        var sut = CreateSut(storage, remote, identityProvider);

        var result = await sut.ImportIdentity(Encoding.UTF8.GetBytes("pokemon-identity"), "password", Catalog(1, 100m));

        result.IsFailure.Should().BeTrue();
        (await storage.Exists(FifoSyncDefaults.IdentityKey)).Value.Should().BeFalse();
    }

    [Fact]
    public async Task Unlock_WhenLocalIsEmptyAndRemoteExists_ShouldDownloadRemoteCatalog()
    {
        var remoteCatalog = Catalog(3, 300m);
        var storage = new InMemoryUserStorage();
        var repository = new JsonEntryCatalogRepository(storage);
        var remote = new FakeRemoteCatalogClient
        {
            RemoteFile = new FifoRemoteCatalogFile(4, "4", EntryCatalogJson.ToBytes(remoteCatalog)),
        };
        var sut = CreateSut(storage, remote, repository: repository);
        await storage.Save(FifoSyncDefaults.IdentityKey, Zafiro.DivineBytes.ByteSource.FromBytes(Encoding.UTF8.GetBytes("identity")));

        var result = await sut.Unlock("password", new EntryCatalog([], []));

        result.IsSuccess.Should().BeTrue();
        var local = await repository.Load();
        local.Value.Should().BeEquivalentTo(remoteCatalog);
        sut.Status.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task Unlock_WhenLocalAndRemoteDifferOnFirstSync_ShouldCreateConflict()
    {
        var storage = new InMemoryUserStorage();
        var remote = new FakeRemoteCatalogClient
        {
            RemoteFile = new FifoRemoteCatalogFile(2, "2", EntryCatalogJson.ToBytes(Catalog(2, 200m))),
        };
        var sut = CreateSut(storage, remote);
        await storage.Save(FifoSyncDefaults.IdentityKey, Zafiro.DivineBytes.ByteSource.FromBytes(Encoding.UTF8.GetBytes("identity")));

        var result = await sut.Unlock("password", Catalog(1, 100m));

        result.IsSuccess.Should().BeTrue();
        sut.Status.Conflict.Should().NotBeNull();
        sut.Status.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task SaveLocalChange_WhenRemoteSaveFails_ShouldKeepLocalChangeDirty()
    {
        var catalog = Catalog(1, 100m);
        var storage = new InMemoryUserStorage();
        var remote = new FakeRemoteCatalogClient { ThrowOnSave = true };
        var sut = CreateSut(storage, remote);
        await sut.CreateIdentity("password", catalog);
        remote.ThrowOnSave = true;

        var result = await sut.SaveLocalChange(Catalog(2, 200m));

        result.IsSuccess.Should().BeTrue();
        sut.Status.IsDirty.Should().BeTrue();
        sut.Status.Conflict.Should().BeNull();
    }

    [Fact]
    public async Task SaveLocalChange_WhenRemoteReturnsConflict_ShouldPreserveLocalAndExposeConflict()
    {
        var storage = new InMemoryUserStorage();
        var repository = new JsonEntryCatalogRepository(storage);
        var remote = new FakeRemoteCatalogClient();
        var sut = CreateSut(storage, remote, repository: repository);
        var original = Catalog(1, 100m);
        await repository.Save(original);
        await sut.CreateIdentity("password", original);
        var localChange = Catalog(2, 200m);
        await repository.Save(localChange);
        remote.RemoteFile = new FifoRemoteCatalogFile(3, "3", EntryCatalogJson.ToBytes(Catalog(3, 300m)));
        remote.ConflictOnSave = true;

        var result = await sut.SaveLocalChange(localChange);

        result.IsSuccess.Should().BeTrue();
        var local = await repository.Load();
        local.Value.Should().BeEquivalentTo(localChange);
        sut.Status.Conflict.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveConflict_WhenUsingRemote_ShouldSaveRemoteCatalogLocally()
    {
        var storage = new InMemoryUserStorage();
        var repository = new JsonEntryCatalogRepository(storage);
        var remoteCatalog = Catalog(3, 300m);
        var remote = new FakeRemoteCatalogClient
        {
            RemoteFile = new FifoRemoteCatalogFile(3, "3", EntryCatalogJson.ToBytes(remoteCatalog)),
        };
        var sut = CreateSut(storage, remote, repository: repository);
        await storage.Save(FifoSyncDefaults.IdentityKey, Zafiro.DivineBytes.ByteSource.FromBytes(Encoding.UTF8.GetBytes("identity")));
        await sut.Unlock("password", Catalog(1, 100m));

        var result = await sut.ResolveConflict(FifoSyncConflictChoice.UseRemote, Catalog(1, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value.HasValue.Should().BeTrue();
        var local = await repository.Load();
        local.Value.Should().BeEquivalentTo(remoteCatalog);
        sut.Status.Conflict.Should().BeNull();
        sut.Status.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveConflict_WhenKeepingLocal_ShouldUploadLocalAgainstRemoteRevision()
    {
        var storage = new InMemoryUserStorage();
        var remote = new FakeRemoteCatalogClient
        {
            RemoteFile = new FifoRemoteCatalogFile(5, "5", EntryCatalogJson.ToBytes(Catalog(5, 500m))),
        };
        var sut = CreateSut(storage, remote);
        await storage.Save(FifoSyncDefaults.IdentityKey, Zafiro.DivineBytes.ByteSource.FromBytes(Encoding.UTF8.GetBytes("identity")));
        var local = Catalog(1, 100m);
        await sut.Unlock("password", local);
        remote.ConflictOnSave = false;

        var result = await sut.ResolveConflict(FifoSyncConflictChoice.KeepLocal, local);

        result.IsSuccess.Should().BeTrue();
        remote.Saves.Last().BaseRevision.Should().Be(5);
        sut.Status.Conflict.Should().BeNull();
        sut.Status.IsDirty.Should().BeFalse();
    }

    private static FifoSyncService CreateSut(
        InMemoryUserStorage storage,
        FakeRemoteCatalogClient remote,
        FakeIdentityProvider? identityProvider = null,
        IEntryCatalogRepository? repository = null)
    {
        return new FifoSyncService(
            repository ?? new JsonEntryCatalogRepository(storage),
            storage,
            identityProvider ?? new FakeIdentityProvider(),
            new FakeRemoteCatalogClientFactory(remote));
    }

    private static async Task<byte[]> LoadStoredIdentity(InMemoryUserStorage storage)
    {
        var load = await storage.Load(FifoSyncDefaults.IdentityKey);
        load.IsSuccess.Should().BeTrue();
        load.Value.HasValue.Should().BeTrue();
        var bytes = await load.Value.Value.ReadAll();
        bytes.IsSuccess.Should().BeTrue();
        return bytes.Value;
    }

    private static EntryCatalog Catalog(decimal units, decimal price)
    {
        return new EntryCatalog([new Entry(new DateTime(2026, 1, 1), units, price)], []);
    }

    private sealed class FakeIdentityProvider : IFifoSyncIdentityProvider
    {
        public bool ImportShouldFail { get; set; }
        public string ImportedAppId { get; set; } = FifoSyncDefaults.AppId;

        public Result<IFifoSyncIdentity> Create(string password)
        {
            return Result.Success<IFifoSyncIdentity>(new FakeIdentity(FifoSyncDefaults.AppId, Encoding.UTF8.GetBytes($"created:{password}")));
        }

        public Result<IFifoSyncIdentity> Import(string password, byte[] protectedExport)
        {
            return ImportShouldFail
                ? Result.Failure<IFifoSyncIdentity>("wrong password")
                : Result.Success<IFifoSyncIdentity>(new FakeIdentity(ImportedAppId, protectedExport));
        }
    }

    private sealed record FakeIdentity(string AppId, byte[] ProtectedExport) : IFifoSyncIdentity;

    private sealed class FakeRemoteCatalogClientFactory(FakeRemoteCatalogClient remote) : IFifoRemoteCatalogClientFactory
    {
        public IFifoRemoteCatalogClient Create(IFifoSyncIdentity identity) => remote;
    }

    private sealed class FakeRemoteCatalogClient : IFifoRemoteCatalogClient
    {
        public FifoRemoteCatalogFile? RemoteFile { get; set; }
        public bool ThrowOnSave { get; set; }
        public bool ConflictOnSave { get; set; }
        public List<(byte[] Bytes, long? BaseRevision)> Saves { get; } = [];

        public Task<FifoRemoteCatalogLoadResult> Load(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<FifoRemoteCatalogLoadResult>(
                RemoteFile is null
                    ? new FifoRemoteCatalogLoadResult.NotFound()
                    : new FifoRemoteCatalogLoadResult.Found(RemoteFile));
        }

        public Task<FifoRemoteCatalogSaveResult> Save(byte[] content, long? baseRevision, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("remote unavailable");
            }

            Saves.Add((content, baseRevision));

            if (ConflictOnSave)
            {
                return Task.FromResult<FifoRemoteCatalogSaveResult>(
                    new FifoRemoteCatalogSaveResult.Conflict(RemoteFile?.Revision ?? 0, RemoteFile?.Cursor ?? "0"));
            }

            var revision = (baseRevision ?? 0) + 1;
            RemoteFile = new FifoRemoteCatalogFile(revision, revision.ToString(), content);

            return Task.FromResult<FifoRemoteCatalogSaveResult>(
                new FifoRemoteCatalogSaveResult.Saved(revision, revision.ToString()));
        }
    }
}

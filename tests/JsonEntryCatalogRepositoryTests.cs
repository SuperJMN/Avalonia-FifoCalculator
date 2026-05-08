using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using System.Text;
using Zafiro.DivineBytes;
using Zafiro.UserStorage;
using Path = Zafiro.DivineBytes.Path;

namespace TestProject1;

public sealed class JsonEntryCatalogRepositoryTests
{
    [Fact]
    public async Task Load_WhenStorageKeyDoesNotExist_ReturnsEmptyCatalog()
    {
        var sut = new JsonEntryCatalogRepository(new InMemoryUserStorage());

        var result = await sut.Load();

        result.IsSuccess.Should().BeTrue();
        result.Value.Inputs.Should().BeEmpty();
        result.Value.Outputs.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTripsCatalog()
    {
        var catalog = new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]);
        var sut = new JsonEntryCatalogRepository(new InMemoryUserStorage());

        var saveResult = await sut.Save(catalog);
        var loadResult = await sut.Load();

        saveResult.IsSuccess.Should().BeTrue();
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Should().BeEquivalentTo(catalog);
    }

    [Fact]
    public async Task Load_FromNamedByteSource_DeserializesCatalog()
    {
        const string json = """
                            {
                              "inputs": [
                                {
                                  "when": "2026-01-10T00:00:00",
                                  "units": 2,
                                  "pricePerUnit": 100
                                }
                              ],
                              "outputs": [
                                {
                                  "when": "2026-02-15T00:00:00",
                                  "units": 1,
                                  "pricePerUnit": 150
                                }
                              ]
                            }
                            """;
        var source = new Resource("import.json", ByteSource.FromBytes(Encoding.UTF8.GetBytes(json)));
        var sut = new JsonEntryCatalogRepository(new InMemoryUserStorage());

        var result = await sut.Load(source);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]));
    }

    [Fact]
    public async Task Save_UsesLogicalStorageKey()
    {
        var storage = new InMemoryUserStorage();
        var sut = new JsonEntryCatalogRepository(storage);

        var result = await sut.Save(new EntryCatalog([], []));

        result.IsSuccess.Should().BeTrue();
        var exists = await storage.Exists(new Path(["database.json"]));
        exists.IsSuccess.Should().BeTrue();
        exists.Value.Should().BeTrue();
    }
}

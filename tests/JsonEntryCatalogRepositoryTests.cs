using FIFOCalculator.Models;
using FIFOCalculator.Persistence;
using System.Text;
using Zafiro.DivineBytes;

namespace TestProject1;

public sealed class JsonEntryCatalogRepositoryTests : IDisposable
{
    private readonly string testDirectory;
    private readonly string testFilePath;

    public JsonEntryCatalogRepositoryTests()
    {
        testDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fifo-calculator-test-{Guid.NewGuid():N}");
        testFilePath = System.IO.Path.Combine(testDirectory, "database.json");
    }

    [Fact]
    public async Task Load_WhenFileDoesNotExist_ReturnsEmptyCatalog()
    {
        var sut = new JsonEntryCatalogRepository(testFilePath);

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
        var sut = new JsonEntryCatalogRepository(testFilePath);

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
        var sut = new JsonEntryCatalogRepository(testFilePath);

        var result = await sut.Load(source);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new EntryCatalog(
            [new Entry(new DateTime(2026, 1, 10), 2, 100m)],
            [new Entry(new DateTime(2026, 2, 15), 1, 150m)]));
    }

    [Fact]
    public void GetDefaultFilePath_UsesUserApplicationData()
    {
        var path = JsonEntryCatalogRepository.GetDefaultFilePath();

        path.Should().EndWith(System.IO.Path.Combine("FIFOCalculator", "database.json"));
        path.Should().Contain(Environment.GetFolderPath(
            OperatingSystem.IsAndroid()
                ? Environment.SpecialFolder.Personal
                : Environment.SpecialFolder.ApplicationData));
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}

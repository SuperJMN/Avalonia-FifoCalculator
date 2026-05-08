using FIFOCalculator.Models;
using FIFOCalculator.Persistence;

namespace TestProject1;

public sealed class JsonEntryCatalogRepositoryTests : IDisposable
{
    private readonly string testDirectory;
    private readonly string testFilePath;

    public JsonEntryCatalogRepositoryTests()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), $"fifo-calculator-test-{Guid.NewGuid():N}");
        testFilePath = Path.Combine(testDirectory, "database.json");
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
    public void GetDefaultFilePath_UsesUserApplicationData()
    {
        var path = JsonEntryCatalogRepository.GetDefaultFilePath();

        path.Should().EndWith(Path.Combine("FIFOCalculator", "database.json"));
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

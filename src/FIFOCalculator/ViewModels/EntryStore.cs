using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;

namespace FIFOCalculator.ViewModels;

internal static class EntryStore
{
    public static Task<Result<EntryCatalog>> Load(Stream stream)
    {
        return Result.Try(async () => (await JsonSerializer.DeserializeAsync<EntryCatalog>(stream))!);
    }

    public static Task<Result> Save(Stream stream, EntryCatalog entries)
    {
        return Result.Try(() => JsonSerializer.SerializeAsync(stream, entries, options: new JsonSerializerOptions(JsonSerializerDefaults.General)));
    }
}
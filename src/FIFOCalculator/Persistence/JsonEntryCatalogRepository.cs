using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using Serilog;
using Zafiro.DivineBytes;

namespace FIFOCalculator.Persistence;

public sealed class JsonEntryCatalogRepository : IEntryCatalogRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string filePath;
    private readonly ILogger logger;

    public JsonEntryCatalogRepository(string? filePath = null, ILogger? logger = null)
    {
        this.filePath = filePath ?? GetDefaultFilePath();
        this.logger = logger ?? Log.Logger;
    }

    public async Task<Result<EntryCatalog>> Load()
    {
        if (!File.Exists(filePath))
        {
            return EmptyCatalog();
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await Load(stream, filePath);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load FIFO calculator database from {Path}", filePath);
            return Result.Failure<EntryCatalog>(ex.Message);
        }
    }

    public async Task<Result<EntryCatalog>> Load(INamedByteSource source)
    {
        var bytes = await source.ReadAll();
        if (bytes.IsFailure)
        {
            return Result.Failure<EntryCatalog>(bytes.Error);
        }

        await using var stream = new MemoryStream(bytes.Value);
        return await Load(stream, source.Name);
    }

    public async Task<Result> Save(EntryCatalog catalog)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, EntryCatalogDto.FromCatalog(catalog), SerializerOptions);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to save FIFO calculator database to {Path}", filePath);
            return Result.Failure(ex.Message);
        }
    }

    public static string GetDefaultFilePath()
    {
        var root = Environment.GetFolderPath(
            OperatingSystem.IsAndroid()
                ? Environment.SpecialFolder.Personal
                : Environment.SpecialFolder.ApplicationData);

        return System.IO.Path.Combine(root, "FIFOCalculator", "database.json");
    }

    private static EntryCatalog EmptyCatalog() => new([], []);

    private async Task<Result<EntryCatalog>> Load(Stream stream, string source)
    {
        try
        {
            var dto = await JsonSerializer.DeserializeAsync<EntryCatalogDto>(stream, SerializerOptions);
            return dto?.ToCatalog() ?? EmptyCatalog();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load FIFO calculator database from {Path}", source);
            return Result.Failure<EntryCatalog>(ex.Message);
        }
    }

    private sealed record EntryCatalogDto
    {
        public List<Entry> Inputs { get; init; } = [];
        public List<Entry> Outputs { get; init; } = [];

        public static EntryCatalogDto FromCatalog(EntryCatalog catalog) => new()
        {
            Inputs = catalog.Inputs,
            Outputs = catalog.Outputs
        };

        public EntryCatalog ToCatalog() => new(Inputs, Outputs);
    }
}

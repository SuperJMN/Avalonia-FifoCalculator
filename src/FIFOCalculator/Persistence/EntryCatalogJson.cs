using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;

namespace FIFOCalculator.Persistence;

public static class EntryCatalogJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] ToBytes(EntryCatalog catalog)
    {
        return JsonSerializer.SerializeToUtf8Bytes(EntryCatalogDto.FromCatalog(catalog), SerializerOptions);
    }

    public static Result<EntryCatalog> FromBytes(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<EntryCatalogDto>(bytes, SerializerOptions);
            return dto?.ToCatalog() ?? EmptyCatalog();
        }
        catch (Exception ex)
        {
            return Result.Failure<EntryCatalog>(ex.Message);
        }
    }

    public static EntryCatalog EmptyCatalog() => new([], []);

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

using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;
using Zafiro.DivineBytes;

namespace FIFOCalculator.Persistence;

public interface IEntryCatalogRepository
{
    Task<Result<EntryCatalog>> Load();

    Task<Result<EntryCatalog>> Load(INamedByteSource source);

    Task<Result> Save(EntryCatalog catalog);
}

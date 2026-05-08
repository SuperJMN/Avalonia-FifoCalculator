using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FIFOCalculator.Models;

namespace FIFOCalculator.Persistence;

public interface IEntryCatalogRepository
{
    Task<Result<EntryCatalog>> Load();

    Task<Result> Save(EntryCatalog catalog);
}

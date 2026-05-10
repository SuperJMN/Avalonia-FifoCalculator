using System;
using System.Linq;
using CSharpFunctionalExtensions;
using Zafiro.Sync.Client;

namespace FIFOCalculator.Sync.Zafiro;

public sealed class ZafiroSyncFifoSyncIdentityProvider : IFifoSyncIdentityProvider
{
    public Result<IFifoSyncIdentity> Create(string password)
    {
        try
        {
            var identity = AppIdentity.Create(FifoSyncDefaults.AppId, FifoSyncDefaults.DisplayName);
            return Result.Success<IFifoSyncIdentity>(new ZafiroSyncFifoSyncIdentity(identity, identity.Export(password)));
        }
        catch (Exception ex)
        {
            return Result.Failure<IFifoSyncIdentity>(ex.Message);
        }
    }

    public Result<IFifoSyncIdentity> Import(string password, byte[] protectedExport)
    {
        try
        {
            var identity = AppIdentity.Import(password, protectedExport);
            return Result.Success<IFifoSyncIdentity>(new ZafiroSyncFifoSyncIdentity(identity, protectedExport));
        }
        catch (Exception ex)
        {
            return Result.Failure<IFifoSyncIdentity>(ex.Message);
        }
    }
}

public sealed class ZafiroSyncFifoSyncIdentity(AppIdentity identity, byte[] protectedExport) : IFifoSyncIdentity
{
    internal AppIdentity Identity { get; } = identity;

    public string AppId => Identity.AppId;

    public byte[] ProtectedExport { get; } = protectedExport.ToArray();
}

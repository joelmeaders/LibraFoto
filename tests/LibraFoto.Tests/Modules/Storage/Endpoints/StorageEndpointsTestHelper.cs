using System.Reflection;
using LibraFoto.Data;
using LibraFoto.Modules.Storage.Features.Providers;
using LibraFoto.Modules.Storage.Features.Sync;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Tests.Modules.Storage.Endpoints;

public static class StorageEndpointsTestHelper
{
    private static readonly MethodInfo _getAllProvidersMethod = typeof(GetStorageProvidersEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _getProviderMethod = typeof(GetStorageProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _createProviderMethod = typeof(CreateStorageProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _updateProviderMethod = typeof(UpdateStorageProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _deleteProviderMethod = typeof(DeleteStorageProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _disconnectProviderMethod = typeof(DisconnectStorageProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _testProviderConnectionMethod = typeof(TestStorageProviderConnectionEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _triggerSyncMethod = typeof(TriggerSyncEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _triggerSyncAllMethod = typeof(TriggerSyncAllEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _getSyncStatusMethod = typeof(GetSyncStatusEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _cancelSyncMethod = typeof(CancelSyncEndpoint).GetMethod("HandleRequest", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _scanProviderMethod = typeof(ScanProviderEndpoint).GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto[]>> GetAllProviders(LibraFotoDbContext db, IStorageProviderFactory factory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto[]>>)_getAllProvidersMethod.Invoke(null, [db, factory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> GetProvider(long id, LibraFotoDbContext db, IStorageProviderFactory factory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_getProviderMethod.Invoke(null, [id, db, factory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Created<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>> CreateProvider(CreateStorageProviderRequest request, LibraFotoDbContext db, IStorageProviderFactory factory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Created<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>>)_createProviderMethod.Invoke(null, [request, db, factory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>> UpdateProvider(long id, UpdateStorageProviderRequest request, LibraFotoDbContext db, IStorageProviderFactory factory, CancellationToken ct)
    {
        var wrapper = new UpdateStorageProviderRequestWrapper
        {
            Id = id,
            Name = request.Name,
            Configuration = request.Configuration,
            IsEnabled = request.IsEnabled
        };

        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>>)_updateProviderMethod.Invoke(null, [wrapper, db, factory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> DeleteProvider(DeleteProviderRequest request, LibraFotoDbContext db, IStorageProviderFactory factory, IConfiguration config, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_deleteProviderMethod.Invoke(null, [request, db, factory, config, loggerFactory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> TriggerSync(long id, SyncRequest? request, ISyncService syncService, CancellationToken ct)
    {
        var syncRequest = request ?? new SyncRequest();
        var endpointRequest = new TriggerSyncRequest
        {
            Id = id,
            FullSync = syncRequest.FullSync,
            RemoveDeleted = syncRequest.RemoveDeleted,
            SkipExisting = syncRequest.SkipExisting,
            MaxFiles = syncRequest.MaxFiles,
            FolderId = syncRequest.FolderId,
            Recursive = syncRequest.Recursive
        };

        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_triggerSyncMethod.Invoke(null, [endpointRequest, syncService, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncStatus>> GetSyncStatus(long id, ISyncService syncService, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncStatus>>)_getSyncStatusMethod.Invoke(null, [id, syncService, ct])!;
    }

    public static Microsoft.AspNetCore.Http.HttpResults.Ok<object> CancelSync(long id, ISyncService syncService)
    {
        return (Microsoft.AspNetCore.Http.HttpResults.Ok<object>)_cancelSyncMethod.Invoke(null, [id, syncService])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>> DisconnectProvider(long id, LibraFotoDbContext db, IStorageProviderFactory factory, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>>)_disconnectProviderMethod.Invoke(null, [id, db, factory, loggerFactory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<object>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> TestProviderConnection(long id, IStorageProviderFactory factory, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<object>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_testProviderConnectionMethod.Invoke(null, [id, factory, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult[]>> TriggerSyncAll(SyncRequest? request, ISyncService syncService, CancellationToken ct)
    {
        var syncRequest = request ?? new SyncRequest();
        var endpointRequest = new TriggerSyncAllRequest
        {
            FullSync = syncRequest.FullSync,
            RemoveDeleted = syncRequest.RemoveDeleted,
            SkipExisting = syncRequest.SkipExisting,
            MaxFiles = syncRequest.MaxFiles,
            FolderId = syncRequest.FolderId,
            Recursive = syncRequest.Recursive
        };

        return (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult[]>>)_triggerSyncAllMethod.Invoke(null, [endpointRequest, syncService, ct])!;
    }

    public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> ScanProvider(long id, ISyncService syncService, CancellationToken ct)
    {
        return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_scanProviderMethod.Invoke(null, [id, syncService, ct])!;
    }
}

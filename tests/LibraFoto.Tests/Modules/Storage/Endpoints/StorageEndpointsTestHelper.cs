using System.Reflection;
using LibraFoto.Data;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Tests.Modules.Storage.Endpoints
{
    public static class StorageEndpointsTestHelper
    {
        private static readonly MethodInfo _getAllProvidersMethod = typeof(StorageEndpoints).GetMethod("GetAllProviders", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _getProviderMethod = typeof(StorageEndpoints).GetMethod("GetProvider", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _createProviderMethod = typeof(StorageEndpoints).GetMethod("CreateProvider", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _updateProviderMethod = typeof(StorageEndpoints).GetMethod("UpdateProvider", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _deleteProviderMethod = typeof(StorageEndpoints).GetMethod("DeleteProvider", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _disconnectProviderMethod = typeof(StorageEndpoints).GetMethod("DisconnectProvider", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _testProviderConnectionMethod = typeof(StorageEndpoints).GetMethod("TestProviderConnection", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _triggerSyncMethod = typeof(StorageEndpoints).GetMethod("TriggerSync", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _triggerSyncAllMethod = typeof(StorageEndpoints).GetMethod("TriggerSyncAll", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _getSyncStatusMethod = typeof(StorageEndpoints).GetMethod("GetSyncStatus", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _cancelSyncMethod = typeof(StorageEndpoints).GetMethod("CancelSync", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo _scanProviderMethod = typeof(StorageEndpoints).GetMethod("ScanProvider", BindingFlags.NonPublic | BindingFlags.Static)!;

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
            return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>>)_updateProviderMethod.Invoke(null, [id, request, db, factory, ct])!;
        }

        public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> DeleteProvider(DeleteProviderRequest request, LibraFotoDbContext db, IStorageProviderFactory factory, IConfiguration config, ILoggerFactory loggerFactory, CancellationToken ct)
        {
            return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_deleteProviderMethod.Invoke(null, [request, db, factory, config, loggerFactory, ct])!;
        }

        public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> TriggerSync(long id, SyncRequest? request, ISyncService syncService, CancellationToken ct)
        {
            return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_triggerSyncMethod.Invoke(null, [id, request, syncService, ct])!;
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
            return (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult[]>>)_triggerSyncAllMethod.Invoke(null, [request, syncService, ct])!;
        }

        public static Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>> ScanProvider(long id, ISyncService syncService, CancellationToken ct)
        {
            return (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>, Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>>)_scanProviderMethod.Invoke(null, [id, syncService, ct])!;
        }
    }
}

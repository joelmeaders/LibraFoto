using LibraFoto.Modules.Admin.Services.Shared;

namespace LibraFoto.Modules.Admin.Features.Shared;

public record AddPhotosToTagRequest(
    long[] PhotoIds
);

public record BulkPhotoRequest(
    long[] PhotoIds
);

public record AddPhotosToAlbumRequest(
    long[] PhotoIds
);

public record RemovePhotosFromAlbumRequest(
    long[] PhotoIds
);

public record AddTagsToPhotosRequest(
    long[] PhotoIds,
    long[] TagIds
);

public record RemoveTagsFromPhotosRequest(
    long[] PhotoIds,
    long[] TagIds
);

public record RemovePhotosFromTagRequest(
    long[] PhotoIds
);

public record ReorderPhotosRequest(
    PhotoOrder[] PhotoOrders
);

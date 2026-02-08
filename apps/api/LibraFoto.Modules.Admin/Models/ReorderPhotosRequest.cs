namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Request to reorder photos in an album.
    /// </summary>
    public record ReorderPhotosRequest(
        PhotoOrder[] PhotoOrders
    );

    /// <summary>
    /// Photo ID and sort order pair.
    /// </summary>
    public record PhotoOrder(
        long PhotoId,
        int SortOrder
    );
}

using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Photo information for list views (optimized for grid display).
    /// </summary>
    public record PhotoListDto(
        long Id,
        string Filename,
        string ThumbnailPath,
        int Width,
        int Height,
        MediaType MediaType,
        DateTime? DateTaken,
        DateTime DateAdded,
        string? Location,
        int AlbumCount,
        int TagCount
    );
}

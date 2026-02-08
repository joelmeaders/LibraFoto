namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Request to add photos to a tag.
    /// </summary>
    public record AddPhotosToTagRequest(
        long[] PhotoIds
    );
}

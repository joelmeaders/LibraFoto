using LibraFoto.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Data;

/// <summary>
/// Main database context for LibraFoto.
/// Uses SQLite for portable, zero-configuration storage.
/// <para>
/// <b>IMPORTANT:</b> After modifying entities, configurations, or relationships, regenerate the compiled model:
/// <code>dotnet ef dbcontext optimize --project apps/api/LibraFoto.Data --startup-project apps/api/LibraFoto.Api</code>
/// </para>
/// </summary>
public class LibraFotoDbContext : DbContext
{
    public LibraFotoDbContext(DbContextOptions<LibraFotoDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Photos and videos in the library.
    /// </summary>
    public DbSet<Photo> Photos => Set<Photo>();

    /// <summary>
    /// Albums for organizing photos.
    /// </summary>
    public DbSet<Album> Albums => Set<Album>();

    /// <summary>
    /// Tags for categorizing photos.
    /// </summary>
    public DbSet<Tag> Tags => Set<Tag>();

    /// <summary>
    /// Junction table for Photo-Album many-to-many relationship.
    /// </summary>
    public DbSet<PhotoAlbum> PhotoAlbums => Set<PhotoAlbum>();

    /// <summary>
    /// Junction table for Photo-Tag many-to-many relationship.
    /// </summary>
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();

    /// <summary>
    /// Users of the application.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Storage provider configurations.
    /// </summary>
    public DbSet<StorageProvider> StorageProviders => Set<StorageProvider>();

    /// <summary>
    /// Display settings for the picture frame.
    /// </summary>
    public DbSet<DisplaySettings> DisplaySettings => Set<DisplaySettings>();

    /// <summary>
    /// Guest upload links.
    /// </summary>
    public DbSet<GuestLink> GuestLinks => Set<GuestLink>();

    /// <summary>
    /// Cached files from cloud storage providers.
    /// </summary>
    public DbSet<CachedFile> CachedFiles => Set<CachedFile>();

    /// <summary>
    /// Google Photos picker sessions.
    /// </summary>
    public DbSet<PickerSession> PickerSessions => Set<PickerSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Photo configuration
        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasIndex(e => e.Filename);
            entity.HasIndex(e => e.DateTaken);
            entity.HasIndex(e => e.DateAdded);
            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => new { e.ProviderId, e.ProviderFileId }).IsUnique();

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.Photos)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Album configuration
        modelBuilder.Entity<Album>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SortOrder);

            entity.HasOne(e => e.CoverPhoto)
                .WithMany(p => p.CoverForAlbums)
                .HasForeignKey(e => e.CoverPhotoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // PhotoAlbum junction table configuration
        modelBuilder.Entity<PhotoAlbum>(entity =>
        {
            entity.HasKey(e => new { e.PhotoId, e.AlbumId });

            entity.HasOne(e => e.Photo)
                .WithMany(p => p.PhotoAlbums)
                .HasForeignKey(e => e.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Album)
                .WithMany(a => a.PhotoAlbums)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.SortOrder);
        });

        // PhotoTag junction table configuration
        modelBuilder.Entity<PhotoTag>(entity =>
        {
            entity.HasKey(e => new { e.PhotoId, e.TagId });

            entity.HasOne(e => e.Photo)
                .WithMany(p => p.PhotoTags)
                .HasForeignKey(e => e.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tag)
                .WithMany(t => t.PhotoTags)
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // StorageProvider configuration
        modelBuilder.Entity<StorageProvider>(entity =>
        {
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsEnabled);
        });

        // DisplaySettings configuration
        modelBuilder.Entity<DisplaySettings>(entity =>
        {
            entity.HasIndex(e => e.IsActive);
        });

        // GuestLink configuration
        modelBuilder.Entity<GuestLink>(entity =>
        {
            // Id is now a string (NanoId), configured in the entity itself
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.CreatedBy)
                .WithMany(u => u.CreatedGuestLinks)
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TargetAlbum)
                .WithMany()
                .HasForeignKey(e => e.TargetAlbumId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CachedFile configuration
        modelBuilder.Entity<CachedFile>(entity =>
        {
            entity.HasIndex(e => e.FileHash).IsUnique();
            entity.HasIndex(e => e.LastAccessedDate);
            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => e.ProviderFileId);
            entity.HasIndex(e => new { e.ProviderId, e.ProviderFileId });

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PickerSession configuration
        modelBuilder.Entity<PickerSession>(entity =>
        {
            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Apply all additional configurations from the Data assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraFotoDbContext).Assembly);
    }
}

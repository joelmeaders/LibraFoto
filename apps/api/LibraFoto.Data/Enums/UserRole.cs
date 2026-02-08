namespace LibraFoto.Data.Enums
{
    /// <summary>
    /// User roles for authorization.
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Guest user with limited access (view only, upload via guest links).
        /// </summary>
        Guest = 0,

        /// <summary>
        /// Editor can manage photos, albums, and tags but not system settings.
        /// </summary>
        Editor = 1,

        /// <summary>
        /// Administrator has full access to all features including user management.
        /// </summary>
        Admin = 2
    }
}

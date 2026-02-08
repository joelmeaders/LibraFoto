using LibraFoto.Modules.Media.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace LibraFoto.Modules.Media.Services
{
    /// <summary>
    /// Service for extracting metadata from images using MetadataExtractor.
    /// </summary>
    public class MetadataService : IMetadataService
    {
        public ImageMetadata ExtractMetadata(Stream stream, string? fileName = null)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                return ParseDirectories(directories);
            }
            catch (Exception)
            {
                return new ImageMetadata();
            }
        }

        public ImageMetadata ExtractMetadata(string filePath)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                return ParseDirectories(directories);
            }
            catch (Exception)
            {
                return new ImageMetadata();
            }
        }

        public Task<ImageMetadata> ExtractMetadataAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ExtractMetadata(stream, fileName), cancellationToken);
        }

        public Task<ImageMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ExtractMetadata(filePath), cancellationToken);
        }

        public DateTime? ExtractDateTaken(Stream stream)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null)
                {
                    if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                    {
                        return dt;
                    }
                    if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out dt))
                    {
                        return dt;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public (double Latitude, double Longitude)? ExtractGpsCoordinates(Stream stream)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
                if (gps != null && gps.TryGetGeoLocation(out var location))
                {
                    return (location.Latitude, location.Longitude);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public (int Width, int Height)? ExtractDimensions(Stream stream)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                int? width = null;
                int? height = null;

                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null)
                {
                    if (subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var w))
                    {
                        width = w;
                    }
                    if (subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var h))
                    {
                        height = h;
                    }
                }

                // Try other directories if not found in EXIF
                if (width == null || height == null)
                {
                    foreach (var dir in directories)
                    {
                        if (width == null)
                        {
                            var widthTag = dir.Tags.FirstOrDefault(t =>
                                t.Name?.Contains("Width", StringComparison.OrdinalIgnoreCase) == true ||
                                t.Name?.Contains("Image Width", StringComparison.OrdinalIgnoreCase) == true);
                            if (widthTag != null && int.TryParse(
                                new string(widthTag.Description?.TakeWhile(char.IsDigit).ToArray() ?? []),
                                out var w))
                            {
                                width = w;
                            }
                        }

                        if (height == null)
                        {
                            var heightTag = dir.Tags.FirstOrDefault(t =>
                                t.Name?.Contains("Height", StringComparison.OrdinalIgnoreCase) == true ||
                                t.Name?.Contains("Image Height", StringComparison.OrdinalIgnoreCase) == true);
                            if (heightTag != null && int.TryParse(
                                new string(heightTag.Description?.TakeWhile(char.IsDigit).ToArray() ?? []),
                                out var h))
                            {
                                height = h;
                            }
                        }
                    }
                }

                if (width.HasValue && height.HasValue)
                {
                    return (width.Value, height.Value);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private ImageMetadata ParseDirectories(IReadOnlyList<MetadataExtractor.Directory> directories)
        {
            DateTime? dateTaken = null;
            int? width = null;
            int? height = null;
            double? latitude = null;
            double? longitude = null;
            double? altitude = null;
            string? cameraMake = null;
            string? cameraModel = null;
            string? lensModel = null;
            int? iso = null;
            double? aperture = null;
            string? exposureTime = null;
            double? focalLength = null;
            int? orientation = null;
            string? colorSpace = null;

            // EXIF IFD0
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null)
            {
                cameraMake = ifd0.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
                cameraModel = ifd0.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
                orientation = ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var o) ? o : null;
            }

            // EXIF SubIFD
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null)
            {
                if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                {
                    dateTaken = dt;
                }
                else if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out dt))
                {
                    dateTaken = dt;
                }

                iso = subIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var i) ? i : null;

                if (subIfd.TryGetRational(ExifDirectoryBase.TagFNumber, out var fNumber))
                {
                    aperture = fNumber.ToDouble();
                }

                exposureTime = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);

                if (subIfd.TryGetRational(ExifDirectoryBase.TagFocalLength, out var fl))
                {
                    focalLength = fl.ToDouble();
                }

                if (subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var w))
                {
                    width = w;
                }
                if (subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var h))
                {
                    height = h;
                }

                lensModel = subIfd.GetDescription(ExifDirectoryBase.TagLensModel)?.Trim();
                colorSpace = subIfd.GetDescription(ExifDirectoryBase.TagColorSpace);
            }

            // GPS
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps != null)
            {
                if (gps.TryGetGeoLocation(out var location))
                {
                    latitude = location.Latitude;
                    longitude = location.Longitude;
                }

                if (gps.TryGetRational(GpsDirectory.TagAltitude, out var alt))
                {
                    altitude = alt.ToDouble();
                }
            }

            // Image dimensions from other sources if not in EXIF
            if (width == null || height == null)
            {
                foreach (var dir in directories)
                {
                    if (width == null)
                    {
                        var widthTag = dir.Tags.FirstOrDefault(t =>
                            t.Name?.Contains("Width", StringComparison.OrdinalIgnoreCase) == true ||
                            t.Name?.Contains("Image Width", StringComparison.OrdinalIgnoreCase) == true);
                        if (widthTag != null && int.TryParse(
                            new string(widthTag.Description?.TakeWhile(char.IsDigit).ToArray() ?? []),
                            out var w))
                        {
                            width = w;
                        }
                    }

                    if (height == null)
                    {
                        var heightTag = dir.Tags.FirstOrDefault(t =>
                            t.Name?.Contains("Height", StringComparison.OrdinalIgnoreCase) == true ||
                            t.Name?.Contains("Image Height", StringComparison.OrdinalIgnoreCase) == true);
                        if (heightTag != null && int.TryParse(
                            new string(heightTag.Description?.TakeWhile(char.IsDigit).ToArray() ?? []),
                            out var h))
                        {
                            height = h;
                        }
                    }
                }
            }

            return new ImageMetadata
            {
                Success = true,
                DateTaken = dateTaken,
                Width = width,
                Height = height,
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                CameraMake = cameraMake,
                CameraModel = cameraModel,
                LensModel = lensModel,
                Iso = iso,
                Aperture = aperture,
                ShutterSpeedFormatted = exposureTime,
                FocalLength = focalLength,
                Orientation = orientation,
                ColorSpace = colorSpace
            };
        }
    }
}

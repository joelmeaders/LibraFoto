using LibraFoto.Modules.Media.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Media.Services
{
    /// <summary>
    /// Service for image processing operations using ImageSharp.
    /// </summary>
    public class ImageProcessor : IImageProcessor
    {
        public async Task<bool> ProcessAsync(
            Stream sourceStream,
            Stream outputStream,
            ProcessingOptions options,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);
                ApplyProcessing(image, options);
                await SaveImageAsync(image, outputStream, options, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ProcessAsync(
            string sourcePath,
            string outputPath,
            ProcessingOptions options,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourcePath, cancellationToken);
                ApplyProcessing(image, options);
                await image.SaveAsync(outputPath, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]?> ProcessToBytesAsync(
            Stream sourceStream,
            ProcessingOptions options,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);
                ApplyProcessing(image, options);

                using var outputStream = new MemoryStream();
                await SaveImageAsync(image, outputStream, options, cancellationToken);
                return outputStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ResizeAsync(
            Stream sourceStream,
            Stream outputStream,
            int maxWidth,
            int maxHeight,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);

                image.Mutate(ctx => ctx
                    .AutoOrient()
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(maxWidth, maxHeight),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));

                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 90 }, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RotateAsync(
            Stream sourceStream,
            Stream outputStream,
            int degrees,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);

                image.Mutate(ctx => ctx.Rotate(degrees));

                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 95 }, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ConvertAsync(
            Stream sourceStream,
            Stream outputStream,
            ImageOutputFormat outputFormat,
            int quality = 85,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);
                image.Mutate(ctx => ctx.AutoOrient());

                await SaveImageToFormatAsync(image, outputStream, outputFormat, quality, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AutoOrientAsync(
            Stream sourceStream,
            Stream outputStream,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream, cancellationToken);
                image.Mutate(ctx => ctx.AutoOrient());
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 95 }, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public (int Width, int Height)? GetDimensions(Stream stream)
        {
            try
            {
                var info = Image.Identify(stream);
                return (info.Width, info.Height);
            }
            catch
            {
                return null;
            }
        }

        public bool IsSupportedFormat(string extension)
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "tiff" or "tif" => true,
                _ => false
            };
        }

        public string GetExtension(ImageOutputFormat format) => format switch
        {
            ImageOutputFormat.Jpeg => "jpg",
            ImageOutputFormat.Png => "png",
            ImageOutputFormat.WebP => "webp",
            ImageOutputFormat.Gif => "gif",
            ImageOutputFormat.Bmp => "bmp",
            _ => "jpg"
        };

        public string GetContentType(ImageOutputFormat format) => format switch
        {
            ImageOutputFormat.Jpeg => "image/jpeg",
            ImageOutputFormat.Png => "image/png",
            ImageOutputFormat.WebP => "image/webp",
            ImageOutputFormat.Gif => "image/gif",
            ImageOutputFormat.Bmp => "image/bmp",
            _ => "image/jpeg"
        };

        private void ApplyProcessing(Image image, ProcessingOptions options)
        {
            image.Mutate(ctx =>
            {
                // Auto-orient first
                if (options.AutoOrient)
                {
                    ctx.AutoOrient();
                }

                // Apply rotation
                if (options.RotationDegrees != 0)
                {
                    ctx.Rotate(options.RotationDegrees);
                }

                // Apply flips
                if (options.FlipHorizontal)
                {
                    ctx.Flip(FlipMode.Horizontal);
                }
                if (options.FlipVertical)
                {
                    ctx.Flip(FlipMode.Vertical);
                }

                // Apply resize
                if (options.MaxDimension.HasValue || options.Width.HasValue || options.Height.HasValue)
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Sampler = KnownResamplers.Lanczos3,
                        Mode = MapResizeMode(options.ResizeMode)
                    };

                    if (options.MaxDimension.HasValue)
                    {
                        resizeOptions.Size = new Size(options.MaxDimension.Value, options.MaxDimension.Value);
                        resizeOptions.Mode = ResizeMode.Max;
                    }
                    else if (options.Width.HasValue && options.Height.HasValue)
                    {
                        resizeOptions.Size = new Size(options.Width.Value, options.Height.Value);
                    }
                    else if (options.Width.HasValue)
                    {
                        resizeOptions.Size = new Size(options.Width.Value, 0);
                    }
                    else if (options.Height.HasValue)
                    {
                        resizeOptions.Size = new Size(0, options.Height.Value);
                    }

                    ctx.Resize(resizeOptions);
                }
            });
        }

        private async Task SaveImageAsync(Image image, Stream outputStream, ProcessingOptions options, CancellationToken cancellationToken)
        {
            var format = options.OutputFormat ?? ImageOutputFormat.Jpeg;
            var quality = format == ImageOutputFormat.WebP ? options.WebPQuality : options.JpegQuality;
            await SaveImageToFormatAsync(image, outputStream, format, quality, cancellationToken);
        }

        private async Task SaveImageToFormatAsync(Image image, Stream outputStream, ImageOutputFormat format, int quality, CancellationToken cancellationToken)
        {
            switch (format)
            {
                case ImageOutputFormat.Jpeg:
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality }, cancellationToken);
                    break;
                case ImageOutputFormat.Png:
                    await image.SaveAsPngAsync(outputStream, new PngEncoder(), cancellationToken);
                    break;
                case ImageOutputFormat.WebP:
                    await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = quality }, cancellationToken);
                    break;
                case ImageOutputFormat.Gif:
                    await image.SaveAsGifAsync(outputStream, cancellationToken);
                    break;
                case ImageOutputFormat.Bmp:
                    await image.SaveAsBmpAsync(outputStream, cancellationToken);
                    break;
                default:
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality }, cancellationToken);
                    break;
            }
        }

        private static ResizeMode MapResizeMode(ImageResizeMode mode) => mode switch
        {
            ImageResizeMode.Max => ResizeMode.Max,
            ImageResizeMode.Crop => ResizeMode.Crop,
            ImageResizeMode.Pad => ResizeMode.Pad,
            ImageResizeMode.Stretch => ResizeMode.Stretch,
            ImageResizeMode.Fill => ResizeMode.Min,
            _ => ResizeMode.Max
        };
    }
}

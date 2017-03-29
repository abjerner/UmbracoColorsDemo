using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SkriftColorsDemo.Models;
using Skybrud.Colors;
using Skybrud.Colors.Html;
using Skybrud.Colors.Wcag;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace SkriftColorsDemo.Utils {

    public abstract class SkybrudMediaUtils {

        #region Properties

        /// <summary>
        /// Gets a reference to the media service.
        /// </summary>
        protected static IMediaService MediaService {
            get { return ApplicationContext.Current.Services.MediaService; }
        }

        /// <summary>
        /// Gets a reference to the file system for "Media".
        /// </summary>
        protected static MediaFileSystem FileSystem {
            get { return FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>(); }
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Calculates the maximum allowed size of an image based on the specified <code>maxWidth</code>, <code>maxHeight</code>, <code>newWidth</code> and <code>newHeight</code>.
        /// </summary>
        /// <param name="maxWidth">The maximum allowed width.</param>
        /// <param name="maxHeight">The maximum allowed height.</param>
        /// <param name="origWidth">The original width of the image.</param>
        /// <param name="origHeight">The original height of the image.</param>
        /// <param name="newWidth">The new width of the image.</param>
        /// <param name="newHeight">The new height of the image.</param>
        public static void CalculateMaximumAllowedSize(int maxWidth, int maxHeight, int origWidth, int origHeight, out int newWidth, out int newHeight) {
            if (origWidth > origHeight) {
                newWidth = maxWidth;
                double factor = newWidth / (double)origWidth;
                newHeight = (int)Math.Round(origHeight * factor);
            } else {
                newHeight = maxHeight;
                double factor = newHeight / (double)origHeight;
                newWidth = (int)Math.Round(origWidth * factor);
            }
        }

        /// <summary>
        /// Generates and returns a new <see cref="Bitmap"/> image that is withtin the specified <code>maxWidth</code>
        /// and <code>maxHeight</code>. If <code>source</code> is already within <code>maxWidth</code> and
        /// <code>maxHeight</code>, <code>source</code> is returned instead.
        /// </summary>
        /// <param name="source">The source <see cref="Bitmap"/> image.</param>
        /// <param name="maxWidth">The maximum allowed width of the thumbnail image.</param>
        /// <param name="maxHeight">The maximum allowed height of the thumbnail image.</param>
        /// <returns>Returns an instance of <see cref="Bitmap"/> representing the thumbnail image.</returns>
        public static Bitmap GetThumbnail(Bitmap source, int maxWidth, int maxHeight) {

            // Get the width and height of the original image
            int origWidth = source.Width;
            int origHeight = source.Height;

            // Return if the image is already lower than the allowed size
            if (origWidth <= maxWidth && origHeight <= maxHeight) return source;

            int newWidth;
            int newHeight;

            // Calculate the "newWidth" and "newHeight" so we keep the aspect ratio
            CalculateMaximumAllowedSize(maxWidth, maxHeight, origWidth, origHeight, out newWidth, out newHeight);

            // Initialize the thumbnail image
            Bitmap thumbnail = new Bitmap(newWidth, newHeight);

            // Use Graphics for manipulating the image
            using (Graphics graphics = Graphics.FromImage(thumbnail)) {

                // Optimize the generation for speed
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.CompositingMode = CompositingMode.SourceCopy;

                // Copy the source image to the thumbnail image (in the correct size)
                graphics.DrawImage(source, 0, 0, newWidth, newHeight);

            }

            return thumbnail;

        }

        public static MostUsedColor[] GetMostUsedColors(Bitmap source, int fuzzyness, int maxWidth, int maxHeight) {

            // Dictionary for keeping track of all the colors
            Dictionary<Color, int> colors = new Dictionary<Color, int>();

            using (Bitmap thumbnail = GetThumbnail(source, maxWidth, maxHeight)) {

                // Iterate through each pixel to find it's color
                for (int x = 0; x < thumbnail.Width; x++) {

                    for (int y = 0; y < thumbnail.Height; y++) {

                        // Get the color from the pixel
                        Color color = thumbnail.GetPixel(x, y);

                        // Apply fuzzyness if greater than 0
                        if (fuzzyness > 0) {
                            int red = color.R / fuzzyness * fuzzyness;
                            int green = color.G / fuzzyness * fuzzyness;
                            int blue = color.B / fuzzyness * fuzzyness;
                            color = Color.FromArgb(red, green, blue);
                        }

                        // Add or increment the color to the dictionary
                        int count;
                        if (colors.TryGetValue(color, out count)) {
                            colors[color]++;
                        } else {
                            colors[color] = 1;
                        }

                    }

                }

            }

            // Convert the dictionary to a array of "MostUsedColor"
            return (
                from color in colors
                orderby color.Value descending
                let rgb = new RgbColor(color.Key.R, color.Key.G, color.Key.B)
                select new MostUsedColor {
                    Rgb = rgb,
                    Hsl = rgb.ToHsl(),
                    Count = color.Value,
                    Wcag = WcagHelpers.GetContrastRatio(rgb, HtmlColors.White)
                }
            ).ToArray();

        }

        public static bool CalculatePrimaryColors(IMedia media) {

            LogHelper.Info(typeof(SkybrudMediaUtils), "CalculatePrimaryColors(" + media.Id + ")");

            // Get the extension of the media
            string umbracoExtension = (media.GetValue<string>(Constants.Conventions.Media.Extension) ?? "").ToLower();
            if (umbracoExtension != "jpg" && umbracoExtension != "jpeg" && umbracoExtension != "png") return true;

            // Get the relative path the the media
            string umbracoFile = media.GetValue<string>(Constants.Conventions.Media.File);
            if (umbracoFile.StartsWith("{")) umbracoFile = JObject.Parse(umbracoFile).GetValue("src") + "";

            // Find the most used colors based on each pixel of the image
            MostUsedColor[] mostUsedColors;
            using (Stream stream = FileSystem.OpenFile(umbracoFile)) {
                using (Bitmap bitmap = new Bitmap(stream)) {
                    mostUsedColors = GetMostUsedColors(bitmap, 25, 512, 512);
                }
            }

            // Find the primary colors based on HSL and WCAG
            MostUsedColor[] primaryColors = (
                from color in mostUsedColors.Take(25)
                where color.Hsl.Saturation > 0.20 && color.Wcag >= 4.5 && color.Wcag <= 10
                orderby color.Wcag descending
                select color
            ).ToArray();

            // Get the currently selected color (if present)
            string currentValue = media.GetValue<string>("color") ?? "";
            Match m1 = Regex.Match(currentValue, "^#([0-9a-f]{6})");

            // Generate the value for the property
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(m1.Success ? "#" + m1.Groups[1].Value : (primaryColors.Length == 0 ? "#666666" : primaryColors[0].Rgb.ToHex()));
            sb.AppendLine(String.Join(" ", from color in mostUsedColors.Take(25) select color.Rgb.ToHex()));
            sb.AppendLine(String.Join(" ", from color in primaryColors select color.Rgb.ToHex()));
            string value = sb.ToString().Replace("\r\n", "\n").Trim();

            // Set the property value
            media.SetValue("color", value);

            // Save the media (and don't raise events since we're already saving)
            MediaService.Save(media, raiseEvents: false);

            // YAY
            return true;

        }

        #endregion

    }

}
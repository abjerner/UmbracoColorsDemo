using Skybrud.Colors;

namespace SkriftColorsDemo.Models {

    public class MostUsedColor {

        /// <summary>
        /// Gets the amount of times this color was used in the image (after fuzzyness has been applied).
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets an instance of <see cref="RgbColor"/> representing the color in the RGB color model.
        /// </summary>
        public RgbColor Rgb { get; set; }

        /// <summary>
        /// Gets an instance of <see cref="HslColor"/> representing the color in the HSL color model.
        /// </summary>
        public HslColor Hsl { get; set; }

        /// <summary>
        /// Gets the WCAG contrast ratio compared to a reference color (eg. white).
        /// </summary>
        public double Wcag { get; set; }


    }

}
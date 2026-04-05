using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RkllmChat.Infra;

public static class ImageProcessor {

    public static byte[] Preprocess(byte[] encodedBytes, int targetWidth, int targetHeight, out int actualLength) {
        actualLength = targetWidth * targetHeight * 3;

        byte[] result = new byte[actualLength];

        using (Image<Rgb24> image = Image.Load<Rgb24>(encodedBytes)) {
            image.Mutate(ctx => {
                ctx.Resize(new ResizeOptions {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Pad,
                    Sampler = KnownResamplers.Lanczos3,
                    PadColor = Color.FromRgb(128, 128, 128)
                });
            });

            image.CopyPixelDataTo(result);
        }

        return result;
    }
}

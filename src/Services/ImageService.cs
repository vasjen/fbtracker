using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using fbtracker.Services.Interfaces;

namespace fbtracker.Services;

public class ImageService : IImageService
{
    public async Task DownloadImageAsync(string directoryPath, string fileName, Uri uri)
    {
        
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "User Agent	Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)");

        string path = Path.Combine(directoryPath, $"{fileName}");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        if (!File.Exists(path))
        {
            byte[] imageBytes = await httpClient.GetByteArrayAsync(uri);
            await File.WriteAllBytesAsync(path, imageBytes);
        }

    }

    public async Task CombineImages(string firstFilePath, string secondFilePath, string outputFilePath)
    {
        if (!File.Exists(outputFilePath))
        {
            using (var fg = Image.Load(firstFilePath))
            {
                using (var bg = Image.Load(secondFilePath))
                {
                    bg.Mutate(ctx =>
                    {
                        // var rect = new Rectangle((bg.Width - fg.Width) / 2, (bg.Height - fg.Height) / 2, fg.Width, fg.Height);
                        bg.Mutate(o => o.DrawImage(fg,new SixLabors.ImageSharp.Point((bg.Width - fg.Width) / 2, (bg.Height - fg.Height) / 2), 1f));
                        // ctx.DrawImage(fg, rect, 1f);
                    });

                    using (var output = new FileStream(outputFilePath, FileMode.Create))
                    {
                        await bg.SaveAsync(output, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                    }
                }
            }
        }
    }
    
}
using System.Drawing;
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
            
            using (var fg = Image.FromFile(firstFilePath))
            {
                using (var bg = Image.FromFile(secondFilePath))
                {
                    
                    Graphics myGraphic = Graphics.FromImage(bg);
                    Rectangle rect = new((bg.Width - fg.Width) / 2, (bg.Height - fg.Height) / 2, fg.Width, fg.Height);
                    myGraphic.DrawImage(fg, rect);
                    MemoryStream output = new System.IO.MemoryStream();
                    bg.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                    await File.WriteAllBytesAsync(outputFilePath, output.ToArray());
                }
            }
        }
    }
    
}
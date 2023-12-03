namespace fbtracker.Services.Interfaces;

public interface IImageService
{
    Task DownloadImageAsync(string directoryPath, string fileName, Uri uri);
    Task CombineImages(string firstFilePath, string secondFilePath, string outputFilePath);
    
}
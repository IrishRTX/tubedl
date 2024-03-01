using NReco.VideoConverter;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;

namespace TubeDL_CLI
{
    internal class Program
    {
        private static async Task Main(string[] arg)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Welcome to TubeDL (Command Line Interface)");
                Console.WriteLine("This tool is used to download video & audio files from YouTube Videos through the command line.");
                Console.WriteLine("");
                Console.WriteLine("-------------------------");
                Console.WriteLine("");

                try
                {
                    Console.WriteLine("Enter video url: ");
                    var videoUrl = Console.ReadLine();

                    Console.WriteLine("Select file type (MP4/MP3): ");
                    var fileType = Console.ReadLine()?.Trim().ToUpper();
                    if (string.IsNullOrEmpty(fileType) || (fileType != "MP4" && fileType != "MP3"))
                    {
                        Console.WriteLine("Invalid file type selected. Defaulting to MP4.");
                        fileType = "MP4";
                    }

                    Console.WriteLine("");
                    Console.WriteLine("-------------------------");
                    Console.WriteLine("");
                    Console.WriteLine($"Attempting to download video as {fileType}");

                    var youtube = new YoutubeClient();
                    var videoInfo = await youtube.Videos.GetAsync(videoUrl);

                    var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var tubeDlFolder = Path.Combine(documentsFolder, "TubeDL");

                    if (!Directory.Exists(tubeDlFolder))
                    {
                        Directory.CreateDirectory(tubeDlFolder);
                    }

                    var fileName = $"{RemoveInvalidFileNameChars(videoInfo.Title)}";

                    string filePath;
                    if (fileType == "MP3")
                    {
                        filePath = Path.Combine(tubeDlFolder, fileName + ".mp3");
                        var audioStreamInfo = (await youtube.Videos.Streams.GetManifestAsync(videoUrl)).GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();
                        if (audioStreamInfo == null)
                        {
                            Console.WriteLine("Audio stream not found.");
                            continue;
                        }
                        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, filePath);
                    }
                    else // MP4
                    {
                        filePath = Path.Combine(tubeDlFolder, fileName + ".mp4");
                        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
                        var videoStreamInfo = streamManifest.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality).FirstOrDefault();
                        var audioStreamInfo = streamManifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();

                        if (videoStreamInfo == null || audioStreamInfo == null)
                        {
                            Console.WriteLine("Video or audio stream not found.");
                            continue;
                        }

                        var videoTask = youtube.Videos.Streams.DownloadAsync(videoStreamInfo, filePath + ".video").AsTask();
                        var audioTask = youtube.Videos.Streams.DownloadAsync(audioStreamInfo, filePath + ".audio").AsTask();

                        await Task.WhenAll(videoTask, audioTask);

                        MergeStreams(filePath + ".video", filePath + ".audio", filePath);
                    }

                    Console.WriteLine("Download completed. Press any key to download another video...");
                    Console.ReadKey();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Video download failed: {e.Message}");
                    Console.WriteLine("Press any key to try again...");
                    Console.ReadKey();
                }
            }
        }

        private static void MergeStreams(string videoPath, string audioPath, string outputPath)
        {
            var ffmpeg = new FFMpegConverter();
            try
            {
                var videoInput = new FFMpegInput(videoPath);
                var audioInput = new FFMpegInput(audioPath);

                ffmpeg.ConvertMedia(new[] { videoInput, audioInput }, outputPath, null, new ConvertSettings { CustomOutputArgs = "-c:v copy -c:a aac -strict experimental -map 0:v:0 -map 1:a:0" });
                File.Delete(videoPath);
                File.Delete(audioPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error merging streams: {ex.Message}");
            }
        }

        private static string RemoveInvalidFileNameChars(string fileName)
        {
            return string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
        }
    }
}

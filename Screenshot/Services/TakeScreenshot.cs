using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Newtonsoft.Json;
using Screenshot.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Screenshot.Services
{
    public class TakeScreenshot : ITakeScreenshot
    {
        private readonly int jpegQuality = ConfigurationManager.CurrentConfig.ScrConfig.JpegQuality;
        private readonly string screenshotFolder = ConfigurationManager.CurrentConfig.ScrConfig.ScreenshotPath;
        private readonly ILogger logger;

        public TakeScreenshot(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();
        }

        void ITakeScreenshot.TakeScreenshot()
        {
            try
            {
                if (!Directory.Exists(screenshotFolder))
                    Directory.CreateDirectory(screenshotFolder);

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = Path.Combine(screenshotFolder, fileName);

                var width = Screen.PrimaryScreen.Bounds.Width;
                var height = Screen.PrimaryScreen.Bounds.Height;

                using (var bmp = new Bitmap(width, height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    }

                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                    if (jpegCodec == null)
                    {
                        logger.LogError("JPEG codec not found, screenshot not saved.");
                        return;
                    }

                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);

                    bmp.Save(filePath, jpegCodec, encoderParameters);
                    logger.LogInformation($"Screenshot saved: {filePath}");
                }

                var helper = new ForegroundWindowHelper();
                string activeWindowTitle = helper.GetActiveWindowTitle();
                string activeProcessName = helper.GetProcessName();

                Screenshots screenshot = new Screenshots
                {
                    PCName = Environment.MachineName,
                    FilePath = filePath,
                    Timestamp = DateTime.Now,
                    ActiveWindowTitle = activeWindowTitle,
                    ActiveProcessName = activeProcessName
                };

                DBContexts.Insert(screenshot);
                logger.LogInformation($"Screenshot inserted into DB: {filePath}");
                logger.LogInformation($"Screenshot inserted into DB: {filePath}");

                var uploadScr = new UploadQueue
                {
                    PayloadType = "screenshot",
                    PayloadJson = JsonConvert.SerializeObject(screenshot),
                    IsSent = false
                };

                DBContexts.Insert(uploadScr);
                logger.LogInformation($"Screenshot added to UploadQueue: {filePath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error taking screenshot: {ex.Message}");
            }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            return Array.Find(
                ImageCodecInfo.GetImageEncoders(),
                codec => codec.MimeType == mimeType);
        }
    }
}

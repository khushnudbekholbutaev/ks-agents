using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebRTC.Services
{
    public class ScreenSharing
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        public async Task StartAsync(string signalingServerUrl)
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            await _ws.ConnectAsync(new Uri(signalingServerUrl), _cts.Token);
            Console.WriteLine("Connected to signaling server.");

            _ = Task.Run(() => SendFramesLoopAsync());
        }

        private async Task SendFramesLoopAsync()
        {
            while (_ws.State == WebSocketState.Open)
            {
                try
                {
                    Bitmap bmp = CaptureScreen();
                    byte[] bytes = BitmapToBytes(bmp);
                    bmp.Dispose();

                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, _cts.Token);

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending frame: " + ex.Message);
                }
            }
        }

        private Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            return bitmap;
        }

        private byte[] BitmapToBytes(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        }
    }
}

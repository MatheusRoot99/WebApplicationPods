using QRCoder;

namespace WebApplicationPods.Helper
{
    public class QRCoder
    {
        private static string MakeQrBase64Png(string text, int pixelsPerModule = 8)
        {
            var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(pixelsPerModule); // 5-10 fica bom
            return Convert.ToBase64String(bytes);
        }
    }
}

using QRCoder;

namespace Auth.API.Services;

public static class TotpQrCodeHelper
{
    public static string ToPngDataUrl(string otpauthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}

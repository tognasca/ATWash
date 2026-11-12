using System.Text;
using System.Text.RegularExpressions;
namespace ATWash.Api;
public static class PixPayload
{
    private static string Field(string id, string value) => id + Encoding.UTF8.GetByteCount(value).ToString("D2") + value;
    public static string Build(string key, string merchantName, string city, decimal amount, string txid)
    {
        if (amount <= 0 || amount > 99999999.99m) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) > 77) throw new ArgumentException("Chave Pix inválida");
        static string Clean(string value, int max)
        {
            var normalized = value.ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var withoutAccents = new string(normalized.Where(ch =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) !=
                System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            var cleaned = Regex.Replace(withoutAccents, "[^A-Z0-9 .-]", "").Trim();
            return cleaned.Length > max ? cleaned[..max] : cleaned;
        }
        var name = Clean(merchantName,25); var town = Clean(city,15);
        if (name.Length == 0 || town.Length == 0) throw new ArgumentException("Nome ou cidade Pix inválidos");
        var account = Field("00","br.gov.bcb.pix") + Field("01",key);
        var body = Field("00","01") + Field("26",account) + Field("52","0000") +
            Field("53","986") + Field("54",amount.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)) +
            Field("58","BR") + Field("59",name) + Field("60",town) +
            Field("62",Field("05",txid));
        var data = body + "6304";
        var crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(data))
        {
            crc ^= b << 8;
            for (var i=0;i<8;i++) crc = (crc & 0x8000)!=0 ? ((crc << 1)^0x1021)&0xFFFF : (crc << 1)&0xFFFF;
        }
        return data + crc.ToString("X4");
    }
}

using System.IO;
using System.Text.Json;
using OMR_staj.Models;

namespace OMR_staj.Json
{
    public class JsonYoneticisi
    {
        public void SablonKaydet(string dosyaYolu, SablonKok sablon)
        {
            string jsonMetni = JsonSerializer.Serialize(sablon, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dosyaYolu, jsonMetni);
        }

        public SablonKok? SablonYukle(string dosyaYolu)
        {
            if (!File.Exists(dosyaYolu)) return null;
            string jsonMetni = File.ReadAllText(dosyaYolu);
            return JsonSerializer.Deserialize<SablonKok>(jsonMetni);
        }
    }
}
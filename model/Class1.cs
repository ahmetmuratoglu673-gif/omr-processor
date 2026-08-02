using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;

namespace OMR_staj.Models
{
    public class AlanSablonu
    {
        public string AlanAdi { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Rectangle? GorselKutu { get; set; }
    }

    public class SablonKok
    {
        public List<AlanSablonu> OptikSablonu { get; set; } = new List<AlanSablonu>();
    }

    public class SoruGoruntuItem
    {
        public string SoruAdi { get; set; } = "";
        public string ResimYolu { get; set; } = "";
        public BitmapImage? Goruntu { get; set; }
    }
}
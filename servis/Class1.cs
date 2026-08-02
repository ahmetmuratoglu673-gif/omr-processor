using System;
using OpenCvSharp;

namespace OMR_staj.Services
{
    public class OptikServisi
    {
        public string TcOku(Mat kimlikResmi)
        {
            if (kimlikResmi.Empty()) return "";

            using Mat griResim = new Mat();
            Cv2.CvtColor(kimlikResmi, griResim, ColorConversionCodes.BGR2GRAY);

            using Mat siyahBeyaz = new Mat();
            // Eşik değerimiz 130. Kalem izleri beyaz, arka plan siyah.
            Cv2.Threshold(griResim, siyahBeyaz, 130, 255, ThresholdTypes.BinaryInv);

            string tcNo = "";

            // SENİN GÖRSELE GÖRE SABİTLENMİŞ DEĞERLER
            int satirSayisi = 10; // 0'dan 9'a kadar 10 satır yuvarlak var
            int sutunSayisi = 5;  // Resimde gördüğümüz 5 haneli öğrenci no sütunu

            int hucreGenisligi = siyahBeyaz.Width / sutunSayisi;
            int gercekHucreYuksekligi = siyahBeyaz.Height / satirSayisi;

            for (int sutun = 0; sutun < sutunSayisi; sutun++)
            {
                int enDoluSatir = -1;
                int maxSiyahPiksel = 0;

                for (int satir = 0; satir < satirSayisi; satir++)
                {
                    int x = sutun * hucreGenisligi;
                    int y = satir * gercekHucreYuksekligi;
                    int w = Math.Min(hucreGenisligi, siyahBeyaz.Width - x);
                    int h = Math.Min(gercekHucreYuksekligi, siyahBeyaz.Height - y);

                    if (w <= 0 || h <= 0) continue;

                    Rect hucreAlani = new Rect(x, y, w, h);
                    using (Mat hucreKaresi = new Mat(siyahBeyaz, hucreAlani))
                    {
                        int doluPikselSayisi = 0;

                        // Piksel piksel tarama:
                        for (int py = 0; py < hucreKaresi.Height; py++)
                        {
                            for (int px = 0; px < hucreKaresi.Width; px++)
                            {
                                byte pikselDegeri = hucreKaresi.At<byte>(py, px);
                                if (pikselDegeri > 128)
                                {
                                    doluPikselSayisi++;
                                }
                            }
                        }

                        if (doluPikselSayisi > maxSiyahPiksel)
                        {
                            maxSiyahPiksel = doluPikselSayisi;
                            enDoluSatir = satir;
                        }
                    }
                }

                int hucreToplamPikseli = hucreGenisligi * gercekHucreYuksekligi;

                // Eğer yeterince karalanmışsa (oran %10) rakamı kabul et
                if (maxSiyahPiksel > (hucreToplamPikseli * 0.10) && enDoluSatir != -1)
                {
                    tcNo += enDoluSatir.ToString();
                }
            }
            return tcNo;
        }
    }
}
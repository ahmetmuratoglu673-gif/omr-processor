using System;
using System.IO;
using System.Net;
using System.Text;
using OMR_staj.Database;

namespace OMR_staj.Web
{
    public class WebSunucusu
    {
        private HttpListener? dinleyici;
        private VeritabaniYoneticisi db = new VeritabaniYoneticisi();

        public void Baslat()
        {
            try
            {
                dinleyici = new HttpListener();
                dinleyici.Prefixes.Add("http://localhost/");
                dinleyici.Start();
                dinleyici.BeginGetContext(SorguyuIsle, dinleyici);
            }
            catch (Exception) { }
        }

        private void SorguyuIsle(IAsyncResult sonuc)
        {
            if (dinleyici == null) return;
            var context = dinleyici.EndGetContext(sonuc);
            dinleyici.BeginGetContext(SorguyuIsle, dinleyici);

            var istek = context.Request;
            var yanit = context.Response;
            string tc = istek.QueryString["tc"] ?? "";
            StringBuilder html = new StringBuilder();

            html.Append("<html><head><meta charset='utf-8'><title>OMR Web Sorgu</title></head><body style='font-family:Arial;'>");
            html.Append("<h2>Optik Soru Goruntuleme</h2>");
            html.Append("<form method='GET'>TC Girin: <input type='text' name='tc' value='" + tc + "'><input type='submit' value='Sorgula'></form><hr/>");

            if (!string.IsNullOrEmpty(tc))
            {
                var sorular = db.TcyeGoreSorulariGetir(tc);
                if (sorular.Count > 0)
                {
                    html.Append("<h3>Bulunan Sorular:</h3><div style='display:flex; flex-wrap:wrap;'>");
                    foreach (var s in sorular)
                    {
                        if (File.Exists(s.ResimYolu))
                        {
                            byte[] baytlar = File.ReadAllBytes(s.ResimYolu);
                            string base64 = Convert.ToBase64String(baytlar);
                            html.Append("<div style='border:1px solid #ccc; margin:5px; padding:5px; text-align:center;'>");
                            html.Append("<b>" + s.SoruAdi + "</b><br/>");
                            html.Append("<img src='data:image/png;base64," + base64 + "' width='200'/>");
                            html.Append("</div>");
                        }
                    }
                    html.Append("</div>");
                }
                else html.Append("<p>Bu TC'ye ait kayit bulunamadi.</p>");
            }

            html.Append("</body></html>");
            byte[] buffer = Encoding.UTF8.GetBytes(html.ToString());
            yanit.ContentLength64 = buffer.Length;
            yanit.OutputStream.Write(buffer, 0, buffer.Length);
            yanit.OutputStream.Close();
        }
    }
}
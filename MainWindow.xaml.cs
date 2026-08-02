using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;
using Microsoft.Data.Sqlite;
using OMR_staj.Database;
using OMR_staj.Json;
using OMR_staj.Models;
using OMR_staj.Services;
using OMR_staj.Web;
using Point = System.Windows.Point;
using Rect = OpenCvSharp.Rect;
using Window = System.Windows.Window;

namespace OMR_staj
{
    public partial class MainWindow : Window
    {
        private bool cizimYapiliyor = false;
        private Point baslangicNoktasi;
        private Rectangle? aktifKutu;
        private List<AlanSablonu> sablonListesi = new List<AlanSablonu>();
        private int soruSayaci = 1;
        private string seciliResimYolu = "";

        // Oranlama için orijinal çözünürlük tutucular
        private double orijinalGenislik = 0;
        private double orijinalYukseklik = 0;

        private List<string> tumTcler = new List<string>();
        private string dbYolu = "omr_veritabani.db";

        private VeritabaniYoneticisi db = new VeritabaniYoneticisi();
        private JsonYoneticisi jsonYoneticisi = new JsonYoneticisi();
        private OptikServisi optikServisi = new OptikServisi();
        private WebSunucusu webSunucusu = new WebSunucusu();

        public MainWindow()
        {
            InitializeComponent();
            db.VeritabaniniKur();
            webSunucusu.Baslat();
        }

        // ==========================================
        // 1. ŞABLON OLUŞTURMA KODLARI
        // ==========================================
        private void btnKagitSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog secici = new OpenFileDialog();
            secici.Filter = "Resimler | *.jpg;*.jpeg;*.png";
            if (secici.ShowDialog() == true)
            {
                seciliResimYolu = secici.FileName;
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(seciliResimYolu);
                bmp.EndInit();
                imgKagit.Source = bmp;

                // Orijinal resim boyutlarını hafızaya alıyoruz
                orijinalGenislik = bmp.PixelWidth;
                orijinalYukseklik = bmp.PixelHeight;
            }
        }

        private void imgKagit_MouseDown(object sender, MouseButtonEventArgs e)
        {
            cizimYapiliyor = true;
            baslangicNoktasi = e.GetPosition(cizimTuvali);
            aktifKutu = new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0)) };
            Canvas.SetLeft(aktifKutu, baslangicNoktasi.X);
            Canvas.SetTop(aktifKutu, baslangicNoktasi.Y);
            cizimTuvali.Children.Add(aktifKutu);
        }

        private void imgKagit_MouseMove(object sender, MouseEventArgs e)
        {
            if (!cizimYapiliyor || aktifKutu == null) return;
            Point guncel = e.GetPosition(cizimTuvali);
            double x = Math.Min(guncel.X, baslangicNoktasi.X);
            double y = Math.Min(guncel.Y, baslangicNoktasi.Y);
            double w = Math.Abs(guncel.X - baslangicNoktasi.X);
            double h = Math.Abs(guncel.Y - baslangicNoktasi.Y);
            Canvas.SetLeft(aktifKutu, x);
            Canvas.SetTop(aktifKutu, y);
            aktifKutu.Width = w;
            aktifKutu.Height = h;
        }

        private void imgKagit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!cizimYapiliyor || aktifKutu == null) return;
            cizimYapiliyor = false;

            double x = Canvas.GetLeft(aktifKutu);
            double y = Canvas.GetTop(aktifKutu);
            double w = aktifKutu.Width;
            double h = aktifKutu.Height;

            if (w <= 0 || h <= 0) { cizimTuvali.Children.Remove(aktifKutu); return; }
            if (cmbAlanlar.SelectedItem == null)
            {
                cizimTuvali.Children.Remove(aktifKutu);
                MessageBox.Show("Alan seciniz.");
                return;
            }

            // Ölçekleme oranı hesaplama
            double oranX = cizimTuvali.ActualWidth > 0 ? orijinalGenislik / cizimTuvali.ActualWidth : 1;
            double oranY = cizimTuvali.ActualHeight > 0 ? orijinalYukseklik / cizimTuvali.ActualHeight : 1;

            ComboBoxItem item = (ComboBoxItem)cmbAlanlar.SelectedItem;
            string secilenMetin = item.Content.ToString() ?? "";
            string alanAdi = secilenMetin;

            if (secilenMetin == "soruno")
            {
                alanAdi = $"soruno_{soruSayaci}";
                soruSayaci++;
            }
            else
            {
                cmbAlanlar.Items.Remove(item);
            }

            AlanSablonu yeni = new AlanSablonu
            {
                AlanAdi = alanAdi,
                X = Math.Round(x * oranX, 2),
                Y = Math.Round(y * oranY, 2),
                Width = Math.Round(w * oranX, 2),
                Height = Math.Round(h * oranY, 2),
                GorselKutu = aktifKutu
            };

            sablonListesi.Add(yeni);
            lstKoordinatlar.Items.Add($"[{yeni.AlanAdi}] X:{yeni.X} Y:{yeni.Y}");
            aktifKutu = null;
            cmbAlanlar.SelectedItem = null;
        }

        private void btnSecileniSil_Click(object sender, RoutedEventArgs e)
        {
            int index = lstKoordinatlar.SelectedIndex;
            if (index == -1) return;
            AlanSablonu silinecek = sablonListesi[index];
            if (silinecek.GorselKutu != null) cizimTuvali.Children.Remove(silinecek.GorselKutu);
            if (!silinecek.AlanAdi.StartsWith("soruno_"))
            {
                cmbAlanlar.Items.Add(new ComboBoxItem { Content = silinecek.AlanAdi });
            }
            sablonListesi.RemoveAt(index);
            lstKoordinatlar.Items.RemoveAt(index);
        }

        private void btnSablonKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (sablonListesi.Count == 0) return;
            string dersAdi = txtDersAdi.Text.Trim();
            if (string.IsNullOrEmpty(dersAdi)) dersAdi = "Turkce";

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "JSON Dosyası (*.json)|*.json";
            sfd.FileName = $"{dersAdi}.json";

            if (sfd.ShowDialog() == true)
            {
                SablonKok kok = new SablonKok { OptikSablonu = sablonListesi };
                jsonYoneticisi.SablonKaydet(sfd.FileName, kok);
                MessageBox.Show("Şablon başarıyla kaydedildi:\n" + sfd.FileName, "Kayıt Başarılı");
            }
        }

        // ==========================================
        // 2. ÜRETİM BANDI (TOPLU OKUMA) KODLARI
        // ==========================================
        private string KlasorSec()
        {
            OpenFileDialog dlg = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Klasor_Sec" };
            if (dlg.ShowDialog() == true) return System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
            return "";
        }

        private void btnJsonSec_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "JSON Dosyaları (*.json)|*.json";
            if (ofd.ShowDialog() == true)
            {
                txtJsonSablon.Text = ofd.FileName;
            }
        }

        private void btnKlasorOkunacak_Click(object sender, RoutedEventArgs e) { txtKlasorOkunacak.Text = KlasorSec(); }
        private void btnKlasorOkunmus_Click(object sender, RoutedEventArgs e) { txtKlasorOkunmus.Text = KlasorSec(); }
        private void btnKlasorHatali_Click(object sender, RoutedEventArgs e) { txtKlasorHatali.Text = KlasorSec(); }

        private void btnTopluIslemBaslat_Click(object sender, RoutedEventArgs e)
        {
            string jsonYolu = txtJsonSablon.Text;
            string okunacakKlasor = txtKlasorOkunacak.Text;
            string arsivKlasor = txtKlasorOkunmus.Text;
            string hataliKlasor = txtKlasorHatali.Text;

            if (string.IsNullOrEmpty(jsonYolu) || string.IsNullOrEmpty(okunacakKlasor) || string.IsNullOrEmpty(arsivKlasor) || string.IsNullOrEmpty(hataliKlasor))
            {
                MessageBox.Show("Lütfen yukarıdaki 4 alanı da doldurun!", "Eksik Bilgi");
                return;
            }

            SablonKok? sablon = jsonYoneticisi.SablonYukle(jsonYolu);
            if (sablon == null)
            {
                MessageBox.Show("JSON Şablonu yüklenemedi. Dosya bozuk veya yanlış olabilir.");
                return;
            }

            string[] tumDosyalar = Directory.GetFiles(okunacakKlasor);
            List<string> resimDosyalari = new List<string>();
            foreach (string d in tumDosyalar)
            {
                string kucukHarf = d.ToLower();
                if (kucukHarf.EndsWith(".jpg") || kucukHarf.EndsWith(".jpeg") || kucukHarf.EndsWith(".png"))
                {
                    resimDosyalari.Add(d);
                }
            }

            if (resimDosyalari.Count == 0)
            {
                MessageBox.Show("Okunacak klasörde hiç resim bulunamadı!");
                return;
            }

            string ders = System.IO.Path.GetFileNameWithoutExtension(jsonYolu);
            int basariliSayisi = 0;
            int hataliSayisi = 0;

            lblDurum.Text = "Durum: İşlem başladı, lütfen bekleyin...";

            foreach (string resimYolu in resimDosyalari)
            {
                string dosyaAdi = System.IO.Path.GetFileName(resimYolu);
                using Mat matris = Cv2.ImRead(resimYolu);
                if (matris.Empty()) continue;

                string okunanTc = "";
                Dictionary<string, string> soruResimleriGecici = new Dictionary<string, string>();

                string geciciKlasor = System.IO.Path.Combine(arsivKlasor, "temp_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(geciciKlasor);

                foreach (var alan in sablon.OptikSablonu)
                {
                    int x = Math.Max(0, (int)alan.X);
                    int y = Math.Max(0, (int)alan.Y);

                    int w = Math.Min((int)alan.Width, matris.Width - x);
                    int h = Math.Min((int)alan.Height, matris.Height - y);

                    if (w <= 0 || h <= 0) continue;

                    Rect rect = new Rect(x, y, w, h);
                    using Mat parca = new Mat(matris, rect);

                    if (alan.AlanAdi == "kimlikno")
                    {
                        okunanTc = optikServisi.TcOku(parca);
                    }
                    else if (alan.AlanAdi.StartsWith("soruno_"))
                    {
                        string soruYol = System.IO.Path.Combine(geciciKlasor, $"{alan.AlanAdi}.png");
                        Cv2.ImWrite(soruYol, parca);
                        soruResimleriGecici.Add(alan.AlanAdi, soruYol);
                    }
                }

                if (string.IsNullOrEmpty(okunanTc))
                {
                    string hataliHedef = System.IO.Path.Combine(hataliKlasor, dosyaAdi);
                    if (File.Exists(hataliHedef)) File.Delete(hataliHedef);
                    File.Move(resimYolu, hataliHedef);

                    Directory.Delete(geciciKlasor, true);
                    hataliSayisi++;
                }
                else
                {
                    string ogrenciArsivKlasoru = System.IO.Path.Combine(arsivKlasor, okunanTc);
                    Directory.CreateDirectory(ogrenciArsivKlasoru);

                    string arsivlenenAnaResim = System.IO.Path.Combine(ogrenciArsivKlasoru, dosyaAdi);
                    if (File.Exists(arsivlenenAnaResim)) File.Delete(arsivlenenAnaResim);
                    File.Move(resimYolu, arsivlenenAnaResim);

                    long ogrId = db.OgrenciKaydet(okunanTc, ders, dosyaAdi, arsivlenenAnaResim);

                    foreach (var soru in soruResimleriGecici)
                    {
                        string kaliciSoruYolu = System.IO.Path.Combine(ogrenciArsivKlasoru, soru.Key + ".png");
                        if (File.Exists(kaliciSoruYolu)) File.Delete(kaliciSoruYolu);
                        File.Move(soru.Value, kaliciSoruYolu);

                        db.SoruKaydet(ogrId, soru.Key, kaliciSoruYolu);
                    }

                    Directory.Delete(geciciKlasor, true);
                    basariliSayisi++;
                }
            }

            lblDurum.Text = $"Durum: Bitti! {basariliSayisi} Başarılı, {hataliSayisi} Hatalı.";
            btnListeyiYenile_Click(sender, e);
            MessageBox.Show($"Üretim Bandı Tamamlandı!\n\nBaşarılı: {basariliSayisi}\nHatalı: {hataliSayisi}", "İşlem Bitti");
        }

        // ==========================================
        // 3. TC SORGULAMA VE YÖNETİM KODLARI
        // ==========================================
        private void btnListeyiYenile_Click(object sender, RoutedEventArgs e)
        {
            lstTcListesi.Items.Clear();
            tumTcler.Clear();

            using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
            {
                baglanti.Open();
                string sql = "SELECT DISTINCT TcKimlik FROM Ogrenciler ORDER BY TcKimlik";
                using (var komut = new SqliteCommand(sql, baglanti))
                {
                    using (var okuyucu = komut.ExecuteReader())
                    {
                        while (okuyucu.Read())
                        {
                            string tc = okuyucu.GetString(0);
                            tumTcler.Add(tc);
                            lstTcListesi.Items.Add(tc);
                        }
                    }
                }
            }
        }

        private void txtTcArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (lstTcListesi == null || tumTcler == null) return;

            string aranan = txtTcArama.Text.ToLower();
            lstTcListesi.Items.Clear();

            foreach (var tc in tumTcler)
            {
                if (tc != null && tc.ToLower().Contains(aranan))
                {
                    lstTcListesi.Items.Add(tc);
                }
            }
        }

        private void lstTcListesi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTcListesi.SelectedItem == null) return;
            string secilenTc = lstTcListesi.SelectedItem.ToString() ?? "";

            var liste = db.TcyeGoreSorulariGetir(secilenTc);
            foreach (var item in liste)
            {
                if (File.Exists(item.ResimYolu))
                {
                    try
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(System.IO.Path.GetFullPath(item.ResimYolu), UriKind.Absolute);
                        bmp.EndInit();
                        item.Goruntu = bmp;
                    }
                    catch (Exception)
                    {
                        item.Goruntu = null;
                    }
                }
            }
            icSorguSonuclari.ItemsSource = liste;
        }

        private void btnTcSil_Click(object sender, RoutedEventArgs e)
        {
            if (lstTcListesi.SelectedItem == null)
            {
                MessageBox.Show("Önce listeden silmek istediğiniz TC'yi seçin!");
                return;
            }

            string secilenTc = lstTcListesi.SelectedItem.ToString() ?? "";
            MessageBoxResult cevap = MessageBox.Show($"{secilenTc} numaralı öğrenciyi silmek istediğinize emin misiniz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (cevap == MessageBoxResult.Yes)
            {
                using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
                {
                    baglanti.Open();
                    string sqlSoru = "DELETE FROM Sorular WHERE OgrenciId IN (SELECT Id FROM Ogrenciler WHERE TcKimlik = @tc)";
                    using (var komut = new SqliteCommand(sqlSoru, baglanti))
                    {
                        komut.Parameters.AddWithValue("@tc", secilenTc);
                        komut.ExecuteNonQuery();
                    }

                    string sqlOgr = "DELETE FROM Ogrenciler WHERE TcKimlik = @tc";
                    using (var komut = new SqliteCommand(sqlOgr, baglanti))
                    {
                        komut.Parameters.AddWithValue("@tc", secilenTc);
                        komut.ExecuteNonQuery();
                    }
                }

                tumTcler.Remove(secilenTc);
                lstTcListesi.Items.Remove(secilenTc);
                icSorguSonuclari.ItemsSource = null;
                MessageBox.Show("Öğrenci veritabanından tamamen silindi!");
            }
        }

        private void btnExceleAktar_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel Dosyası (*.xls)|*.xls";
            sfd.FileName = "OMR_Ogrenci_Raporu.xls";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    System.Text.StringBuilder html = new System.Text.StringBuilder();
                    // Excel'in Türkçe karakterleri tanıması için meta etiketini güçlendirdik
                    html.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns='http://www.w3.org/TR/REC-html40'>");
                    html.AppendLine("<head><meta http-equiv='Content-Type' content='text/html; charset=utf-8'></head><body><table border='1'>");
                    html.AppendLine("<tr style='background-color:#4F81BD; color:white; font-weight:bold; height:30px;'><th>TC Kimlik</th><th>Ders</th><th>Dosya Adi</th><th>Resim Klasörü (Tıkla Aç)</th></tr>");

                    using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
                    {
                        baglanti.Open();
                        string sql = "SELECT TcKimlik, Ders, DosyaAdi FROM Ogrenciler ORDER BY TcKimlik";
                        using (var komut = new SqliteCommand(sql, baglanti))
                        {
                            using (var okuyucu = komut.ExecuteReader())
                            {
                                while (okuyucu.Read())
                                {
                                    string tc = okuyucu.GetString(0);
                                    string ders = okuyucu.GetString(1);
                                    string dosya = okuyucu.GetString(2);

                                    string klasorAdi = System.IO.Path.GetFileNameWithoutExtension(dosya);
                                    string tamKlasorYolu = System.IO.Path.GetFullPath(System.IO.Path.Combine("Arsiv", klasorAdi));

                                    html.AppendLine($"<tr><td>{tc}</td><td>{ders}</td><td>{dosya}</td><td><a href='{tamKlasorYolu}'>📂 Klasörü Aç</a></td></tr>");
                                }
                            }
                        }
                    }
                    html.AppendLine("</table></body></html>");

                    // true parametresi (BOM) Excel'in dosyayı UTF-8 formatında kusursuz okumasını sağlar
                    File.WriteAllText(sfd.FileName, html.ToString(), new System.Text.UTF8Encoding(true));
                    MessageBox.Show("Excel raporu ve ders adları başarıyla güncellendi!", "Bilgi");
                }
                catch (IOException)
                {
                    // Eğer dosya Excel'de açıksa program çökmez, bu uyarıyı verir:
                    MessageBox.Show("HATA: Kaydetmeye çalıştığınız Excel dosyası şu an arka planda açık!\n\nLütfen açık olan Excel dosyasını kapatıp tekrar deneyin.", "Dosya Açık", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Bilinmeyen bir hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SoruResim_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Tag is string yol && File.Exists(yol))
            {
                Window win = new Window { Title = "Soru Buyuk", Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                win.Content = new Image { Source = new BitmapImage(new Uri(System.IO.Path.GetFullPath(yol), UriKind.Absolute)), Stretch = Stretch.Uniform };
                win.ShowDialog();
            }
        }
    }
}
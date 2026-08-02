using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using OMR_staj.Models;

namespace OMR_staj.Database
{
    public class VeritabaniYoneticisi
    {
        private string dbYolu = "omr_veritabani.db";

        public void VeritabaniniKur()
        {
            using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
            {
                baglanti.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS Ogrenciler (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TcKimlik TEXT,
                        Ders TEXT,
                        DosyaAdi TEXT,
                        AnaResim TEXT
                    );
                    CREATE TABLE IF NOT EXISTS Sorular (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OgrenciId INTEGER,
                        SoruAdi TEXT,
                        ResimYolu TEXT,
                        FOREIGN KEY(OgrenciId) REFERENCES Ogrenciler(Id)
                    );";
                using (var komut = new SqliteCommand(sql, baglanti))
                {
                    komut.ExecuteNonQuery();
                }
            }
        }

        public long OgrenciKaydet(string tc, string ders, string dosyaAdi, string anaResim)
        {
            VeritabaniniKur();
            using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
            {
                baglanti.Open();
                string sql = "INSERT INTO Ogrenciler (TcKimlik, Ders, DosyaAdi, AnaResim) VALUES (@tc, @ders, @dosya, @anaResim); SELECT last_insert_rowid();";
                using (var komut = new SqliteCommand(sql, baglanti))
                {
                    komut.Parameters.AddWithValue("@tc", tc);
                    komut.Parameters.AddWithValue("@ders", ders);
                    komut.Parameters.AddWithValue("@dosya", dosyaAdi);
                    komut.Parameters.AddWithValue("@anaResim", anaResim);
                    return (long)komut.ExecuteScalar()!;
                }
            }
        }

        public void SoruKaydet(long ogrenciId, string soruAdi, string resimYolu)
        {
            using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
            {
                baglanti.Open();
                string sql = "INSERT INTO Sorular (OgrenciId, SoruAdi, ResimYolu) VALUES (@ogrId, @soruAdi, @resimYolu);";
                using (var komut = new SqliteCommand(sql, baglanti))
                {
                    komut.Parameters.AddWithValue("@ogrId", ogrenciId);
                    komut.Parameters.AddWithValue("@soruAdi", soruAdi);
                    komut.Parameters.AddWithValue("@resimYolu", resimYolu);
                    komut.ExecuteNonQuery();
                }
            }
        }

        public List<SoruGoruntuItem> TcyeGoreSorulariGetir(string tc)
        {
            VeritabaniniKur();
            List<SoruGoruntuItem> liste = new List<SoruGoruntuItem>();
            using (var baglanti = new SqliteConnection($"Data Source={dbYolu}"))
            {
                baglanti.Open();
                string sql = @"
                    SELECT s.SoruAdi, s.ResimYolu 
                    FROM Sorular s
                    INNER JOIN Ogrenciler o ON s.OgrenciId = o.Id
                    WHERE o.TcKimlik = @tc
                    ORDER BY s.Id";
                using (var komut = new SqliteCommand(sql, baglanti))
                {
                    komut.Parameters.AddWithValue("@tc", tc);
                    using (var okuyucu = komut.ExecuteReader())
                    {
                        while (okuyucu.Read())
                        {
                            liste.Add(new SoruGoruntuItem { SoruAdi = okuyucu.GetString(0), ResimYolu = okuyucu.GetString(1) });
                        }
                    }
                }
            }
            return liste;
        }
    }
}
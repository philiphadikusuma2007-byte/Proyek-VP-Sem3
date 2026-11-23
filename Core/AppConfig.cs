using System.Text.Json;
using MySql.Data.MySqlClient;

namespace SuryaMandiriPrint;

/// <summary>Pengaturan koneksi ke server MySQL (dibaca dari koneksi.json di folder aplikasi).</summary>
public class AppConfig
{
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "db_percetakan";
    public string User { get; set; } = "sm_app";
    public string Password { get; set; } = "SuryaMandiri#2026";
    /// <summary>Folder penyimpanan file pesanan pada server (boleh path jaringan, mis. \\SERVER\FilePesanan).</summary>
    public string FolderFile { get; set; } = @"C:\SuryaMandiri\FilePesanan";

    public static AppConfig Current { get; private set; } = new AppConfig();

    static string LokasiFile => System.IO.Path.Combine(AppContext.BaseDirectory, "koneksi.json");

    public static void Muat()
    {
        try
        {
            if (File.Exists(LokasiFile))
                Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(LokasiFile)) ?? new AppConfig();
        }
        catch { Current = new AppConfig(); }
    }

    public static void Simpan()
    {
        File.WriteAllText(LokasiFile, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string ConnectionString
    {
        get
        {
            var b = new MySqlConnectionStringBuilder
            {
                Server = Server,
                Port = (uint)Port,
                Database = Database,
                UserID = User,
                Password = Password,
                CharacterSet = "utf8mb4",
                AllowPublicKeyRetrieval = true,
                ConnectionTimeout = 8,
                DefaultCommandTimeout = 60
            };
            return b.ConnectionString;
        }
    }
}

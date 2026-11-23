using System.Data;
using MySql.Data.MySqlClient;

namespace SuryaMandiriPrint;

/// <summary>Akses database MySQL: query, perintah, dan TRANSACTION.</summary>
public static class Db
{
    public static MySqlConnection Buka()
    {
        var c = new MySqlConnection(AppConfig.Current.ConnectionString);
        c.Open();
        return c;
    }

    static MySqlCommand Buat(string sql, MySqlConnection c, MySqlTransaction t, (string, object)[] p)
    {
        var cmd = new MySqlCommand(sql, c, t);
        foreach (var (nama, nilai) in p) cmd.Parameters.AddWithValue(nama, nilai ?? DBNull.Value);
        return cmd;
    }

    // ---------- tanpa transaksi (koneksi dibuka & ditutup otomatis) ----------
    public static DataTable Tabel(string sql, params (string, object)[] p)
    {
        using var c = Buka();
        return Tabel(c, null, sql, p);
    }

    public static object Nilai(string sql, params (string, object)[] p)
    {
        using var c = Buka();
        return Nilai(c, null, sql, p);
    }

    public static int Jalankan(string sql, params (string, object)[] p)
    {
        using var c = Buka();
        return Jalankan(c, null, sql, p);
    }

    // ---------- dengan koneksi/transaksi yang sudah ada ----------
    public static DataTable Tabel(MySqlConnection c, MySqlTransaction t, string sql, params (string, object)[] p)
    {
        using var cmd = Buat(sql, c, t, p);
        using var da = new MySqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);
        return dt;
    }

    public static object Nilai(MySqlConnection c, MySqlTransaction t, string sql, params (string, object)[] p)
    {
        using var cmd = Buat(sql, c, t, p);
        return cmd.ExecuteScalar();
    }

    public static int Jalankan(MySqlConnection c, MySqlTransaction t, string sql, params (string, object)[] p)
    {
        using var cmd = Buat(sql, c, t, p);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>INSERT dan kembalikan id auto-increment-nya.</summary>
    public static long Sisip(MySqlConnection c, MySqlTransaction t, string sql, params (string, object)[] p)
    {
        using var cmd = Buat(sql, c, t, p);
        cmd.ExecuteNonQuery();
        return cmd.LastInsertedId;
    }

    public static long Sisip(string sql, params (string, object)[] p)
    {
        using var c = Buka();
        return Sisip(c, null, sql, p);
    }

    /// <summary>Jalankan beberapa perintah sebagai satu TRANSACTION: semua berhasil (COMMIT) atau semua dibatalkan (ROLLBACK).</summary>
    public static void Transaksi(Action<MySqlConnection, MySqlTransaction> kerja)
    {
        using var c = Buka();
        using var t = c.BeginTransaction();
        try
        {
            kerja(c, t);
            t.Commit();
        }
        catch
        {
            try { t.Rollback(); } catch { }
            throw;
        }
    }
}

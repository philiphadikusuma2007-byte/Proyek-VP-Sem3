namespace SuryaMandiriPrint;

public static class RoleNama
{
    public const string Depan = "Admin Depan";
    public const string Belakang = "Admin Belakang";
    public const string Kasir = "Kasir";
}

/// <summary>Pengguna yang sedang login + pemeriksaan hak akses per role.</summary>
public static class Sesi
{
    public static int IdUser { get; set; }
    public static string Username { get; set; }
    public static string Nama { get; set; }
    public static string Role { get; set; }

    public static bool Ada(params string[] peran) => Array.IndexOf(peran, Role) >= 0;

    /// <summary>Dipanggil di awal setiap form / aksi: melempar error bila role tidak berhak.</summary>
    public static void Wajib(params string[] peran)
    {
        if (!Ada(peran))
            throw new UnauthorizedAccessException("Role \"" + Role + "\" tidak memiliki hak untuk fitur ini.");
    }
}

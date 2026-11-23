using System.Globalization;

namespace SuryaMandiriPrint;

/// <summary>Pengelompokan laporan: 0 = Harian, 1 = Mingguan (Senin-Minggu), 2 = Bulanan.</summary>
public static class PeriodeHelper
{
    public static readonly string[] Nama = { "Harian", "Mingguan", "Bulanan" };

    /// <summary>Ekspresi SQL yang menghasilkan tanggal awal periode untuk sebuah kolom tanggal.</summary>
    public static string KunciSql(int jenis, string kolom) => jenis switch
    {
        0 => "DATE(" + kolom + ")",
        1 => "DATE_SUB(DATE(" + kolom + "), INTERVAL WEEKDAY(" + kolom + ") DAY)",
        _ => "DATE_SUB(DATE(" + kolom + "), INTERVAL DAYOFMONTH(" + kolom + ") - 1 DAY)"
    };

    /// <summary>Label tampilan dari kunci 'yyyy-MM-dd' hasil query.</summary>
    public static string Label(int jenis, string kunci)
    {
        var d = DateTime.ParseExact(kunci, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return jenis switch
        {
            0 => d.ToString("dd/MM/yyyy"),
            1 => "Mgg " + d.ToString("dd/MM") + " - " + d.AddDays(6).ToString("dd/MM/yy"),
            _ => d.ToString("MMMM yyyy", UI.ID)
        };
    }
}

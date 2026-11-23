using System.Data;
using FastReport;
using FastReport.Utils;

namespace SuryaMandiriPrint;

/// <summary>Definisi satu kolom pada tabel laporan (lebar dalam milimeter).</summary>
public class KolomLaporan
{
    public string Nama;
    public string Judul;
    public float Lebar;
    public HorzAlign Rata;
    public Func<object, string> Fmt;

    public KolomLaporan(string nama, string judul, float lebar, HorzAlign rata = HorzAlign.Left, Func<object, string> fmt = null)
    {
        Nama = nama; Judul = judul; Lebar = lebar; Rata = rata; Fmt = fmt;
    }
}

/// <summary>Satu bagian tabel dalam laporan (misalnya "Rekap per Periode").</summary>
public class BagianLaporan
{
    public string Judul;
    public DataTable Tabel;
    public List<KolomLaporan> Kolom = new();
}

/// <summary>
/// Pembuat laporan FastReport lewat kode: nota/struk, Sales Report, Print Volume Report.
/// Grafik dibuat dengan Chart control lalu disisipkan sebagai gambar (PictureObject).
/// Laporan bisa di-print / di-export (PDF, Excel, dll.) dari jendela preview FastReport.
/// </summary>
public static class ReportService
{
    static float MM => Units.Millimeters;

    // ================================================================= helper objek
    static TextObject Teks(BandBase band, float x, float y, float w, float h, string teks,
                           float ukuran = 9, bool tebal = false, HorzAlign rata = HorzAlign.Left,
                           bool garis = false, bool ekspresi = false, string font = "Arial")
    {
        var t = new TextObject();
        band.Objects.Add(t);
        t.CreateUniqueName();
        t.Bounds = new RectangleF(x * MM, y * MM, w * MM, h * MM);
        t.Text = teks;
        t.AllowExpressions = ekspresi;
        t.Font = new Font(font, ukuran, tebal ? FontStyle.Bold : FontStyle.Regular);
        t.HorzAlign = rata;
        t.VertAlign = VertAlign.Center;
        t.WordWrap = true;
        if (garis) t.Border.Lines = BorderLines.All;
        return t;
    }

    // ================================================================= NOTA / STRUK
    /// <summary>Nota pembayaran ala struk (kertas 80 mm) untuk sebuah transaksi penjualan.</summary>
    public static void TampilkanNota(int idPenjualan)
    {
        try
        {
            var h = Db.Tabel(
                "SELECT p.tanggal_transaksi, p.total_bayar, p.uang_diterima, p.kembalian, p.metode_pembayaran, " +
                "p.status_pembayaran, p.jatuh_tempo, n.no_nota, n.nama_pelanggan, u.nama_lengkap AS kasir " +
                "FROM penjualan p JOIN nota n ON n.id_nota = p.id_nota " +
                "LEFT JOIN `user` u ON u.id_user = p.id_user WHERE p.id_penjualan = @id", ("@id", idPenjualan));
            if (h.Rows.Count == 0) { UI.Info("Data transaksi tidak ditemukan."); return; }
            var r = h.Rows[0];

            var items = Db.Tabel(
                "SELECT k.nama_kertas, k.kategori, k.satuan, d.sisi, d.jumlah_halaman, d.jumlah_copy, d.harga_satuan, d.subtotal " +
                "FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas " +
                "JOIN penjualan p ON p.id_nota = d.id_nota WHERE p.id_penjualan = @id ORDER BY d.id_detail", ("@id", idPenjualan));

            var t = new DataTable("Item");
            t.Columns.Add("Nama"); t.Columns.Add("Rincian"); t.Columns.Add("Subtotal");
            foreach (DataRow x in items.Rows)
            {
                string nama = Convert.ToString(x["nama_kertas"]);
                if (Convert.ToString(x["kategori"]) == "LEMBAR")
                    nama += UI.Int(x["sisi"]) == 2 ? " (bolak-balik)" : " (1 hal)";
                if (nama.Length > 38) nama = nama.Substring(0, 38);
                int qty = UI.Int(x["jumlah_halaman"]) * UI.Int(x["jumlah_copy"]);
                t.Rows.Add(nama, qty.ToString("N0", UI.ID) + " " + x["satuan"] + " x @" + UI.Angka(x["harga_satuan"]), UI.Rp(x["subtotal"]));
            }

            var ds = new DataSet("ds");
            ds.Tables.Add(t);
            var rpt = new Report();
            rpt.RegisterData(ds, "ds");
            rpt.GetDataSource("Item").Enabled = true;

            var page = new ReportPage();
            rpt.Pages.Add(page);
            page.CreateUniqueName();
            page.PaperWidth = 80;
            page.PaperHeight = 150;
            page.LeftMargin = 3; page.RightMargin = 3; page.TopMargin = 3; page.BottomMargin = 3;
            page.UnlimitedHeight = true;           // struk: tinggi menyesuaikan jumlah item
            const float W = 74;                    // lebar area cetak (mm)
            const string F = "Courier New";

            // ---- kepala struk ----
            page.ReportTitle = new ReportTitleBand();
            var title = page.ReportTitle;
            title.CreateUniqueName();
            float y = 0;
            Teks(title, 0, y, W, 8, "SURYA MANDIRI", 15, true, HorzAlign.Center, false, false, F); y += 8;
            Teks(title, 0, y, W, 4.5f, "Digital Print A3+", 8, false, HorzAlign.Center, false, false, F); y += 4.5f;
            Teks(title, 0, y, W, 4.5f, "Petemon Barat No 64 Surabaya", 8, false, HorzAlign.Center, false, false, F); y += 4.5f;
            Teks(title, 0, y, W, 4.5f, "WA 085 102 111 258", 8, false, HorzAlign.Center, false, false, F); y += 6;
            Teks(title, 0, y, W, 4.5f, "REG  " + ((DateTime)r["tanggal_transaksi"]).ToString("dd-MM-yyyy HH:mm"), 8, false, HorzAlign.Left, false, false, F); y += 4.5f;
            Teks(title, 0, y, W, 4.5f, "No   " + r["no_nota"], 8, false, HorzAlign.Left, false, false, F); y += 4.5f;
            Teks(title, 0, y, W, 4.5f, "Plgn " + r["nama_pelanggan"], 8, false, HorzAlign.Left, false, false, F); y += 4.5f;
            Teks(title, 0, y, W, 4.5f, "Kasir " + r["kasir"], 8, false, HorzAlign.Left, false, false, F); y += 5;
            Teks(title, 0, y, W, 4, "--------------------------------------", 8, false, HorzAlign.Left, false, false, F); y += 4;
            title.Height = y * MM;

            // ---- baris item ----
            var db = new DataBand();
            page.Bands.Add(db);
            db.CreateUniqueName();
            db.DataSource = rpt.GetDataSource("Item");
            db.Height = 10.5f * MM;
            Teks(db, 0, 0, W, 5, "[Item.Nama]", 8, true, HorzAlign.Left, false, true, F);
            Teks(db, 0, 5, 44, 5, "[Item.Rincian]", 8, false, HorzAlign.Left, false, true, F);
            Teks(db, 44, 5, 30, 5, "[Item.Subtotal]", 8, false, HorzAlign.Right, false, true, F);

            // ---- ringkasan pembayaran ----
            page.ReportSummary = new ReportSummaryBand();
            var sum = page.ReportSummary;
            sum.CreateUniqueName();
            y = 0;
            Teks(sum, 0, y, W, 4, "--------------------------------------", 8, false, HorzAlign.Left, false, false, F); y += 4;
            Teks(sum, 0, y, 30, 6, "TL", 11, true, HorzAlign.Left, false, false, F);
            Teks(sum, 30, y, 44, 6, UI.Rp(r["total_bayar"]), 11, true, HorzAlign.Right, false, false, F); y += 6;

            string metode = Convert.ToString(r["metode_pembayaran"]);
            if (Convert.ToString(r["status_pembayaran"]) == "Lunas")
            {
                Teks(sum, 0, y, 30, 5, metode.ToUpper(), 9, false, HorzAlign.Left, false, false, F);
                Teks(sum, 30, y, 44, 5, UI.Rp(r["uang_diterima"]), 9, false, HorzAlign.Right, false, false, F); y += 5;
                Teks(sum, 0, y, 30, 5, "KEMBALI", 9, false, HorzAlign.Left, false, false, F);
                Teks(sum, 30, y, 44, 5, UI.Rp(r["kembalian"]), 9, false, HorzAlign.Right, false, false, F); y += 6;
                Teks(sum, 0, y, W, 5, "*** LUNAS ***", 10, true, HorzAlign.Center, false, false, F); y += 6;
            }
            else
            {
                string jt = r["jatuh_tempo"] is DateTime d ? d.ToString("dd-MM-yyyy") : "-";
                Teks(sum, 0, y, W, 5, "BELUM LUNAS (TEMPO)", 10, true, HorzAlign.Center, false, false, F); y += 5.5f;
                Teks(sum, 0, y, W, 5, "Jatuh tempo: " + jt, 9, false, HorzAlign.Center, false, false, F); y += 6;
            }
            Teks(sum, 0, y, W, 5, "Terima kasih atas kunjungan Anda", 8, false, HorzAlign.Center, false, false, F); y += 6;
            sum.Height = y * MM;

            rpt.Show();
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    // ================================================================= LAPORAN UMUM (tabel + grafik)
    /// <summary>
    /// Membuat laporan A4: judul, periode, ringkasan, grafik (gambar), lalu satu atau lebih tabel.
    /// </summary>
    public static void TampilkanLaporan(string judul, string subJudul, IList<string> ringkasan, Image grafik,
                                        IList<BagianLaporan> bagian)
    {
        try
        {
            var rpt = new Report();
            var ds = new DataSet("ds");

            // tabel sumber -> tabel string terformat (nama tabel T1, T2, ...)
            for (int i = 0; i < bagian.Count; i++)
            {
                var b = bagian[i];
                var t = new DataTable("T" + (i + 1));
                foreach (var k in b.Kolom) t.Columns.Add(k.Nama, typeof(string));
                foreach (DataRow row in b.Tabel.Rows)
                {
                    var baru = t.NewRow();
                    foreach (var k in b.Kolom)
                    {
                        object v = row[k.Nama];
                        baru[k.Nama] = k.Fmt != null ? k.Fmt(v) : (v == null || v is DBNull ? "" : Convert.ToString(v));
                    }
                    t.Rows.Add(baru);
                }
                ds.Tables.Add(t);
            }
            rpt.RegisterData(ds, "ds");
            for (int i = 0; i < bagian.Count; i++) rpt.GetDataSource("T" + (i + 1)).Enabled = true;

            var page = new ReportPage();
            rpt.Pages.Add(page);
            page.CreateUniqueName();                 // A4 portrait, margin 10 mm -> lebar isi 190 mm
            const float W = 190;

            // ---- kepala laporan ----
            page.ReportTitle = new ReportTitleBand();
            var title = page.ReportTitle;
            title.CreateUniqueName();
            float y = 0;
            Teks(title, 0, y, W, 9, "SURYA MANDIRI DIGITAL PRINT", 11, true, HorzAlign.Center); y += 6;
            Teks(title, 0, y, W, 9, judul, 16, true, HorzAlign.Center); y += 10;
            Teks(title, 0, y, W, 6, subJudul, 10, false, HorzAlign.Center); y += 8;
            foreach (var baris in ringkasan)
            {
                Teks(title, 10, y, W - 20, 5.5f, baris, 10); y += 5.5f;
            }
            y += 3;
            if (grafik != null)
            {
                var pic = new PictureObject();
                title.Objects.Add(pic);
                pic.CreateUniqueName();
                pic.Bounds = new RectangleF(0, y * MM, W * MM, 75 * MM);
                pic.Image = grafik;
                pic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
                y += 78;
            }
            title.Height = (y + 2) * MM;

            // ---- tabel-tabel ----
            for (int i = 0; i < bagian.Count; i++)
            {
                var b = bagian[i];
                string tbl = "T" + (i + 1);

                var db = new DataBand();
                page.Bands.Add(db);
                db.CreateUniqueName();
                db.DataSource = rpt.GetDataSource(tbl);
                db.Height = 6 * MM;

                db.Header = new DataHeaderBand();
                db.Header.CreateUniqueName();
                db.Header.Height = 15 * MM;
                db.Header.RepeatOnEveryPage = true;
                Teks(db.Header, 0, 1, W, 6.5f, b.Judul, 11, true);

                float x = 0;
                foreach (var k in b.Kolom)
                {
                    var hd = Teks(db.Header, x, 8, k.Lebar, 6.5f, k.Judul, 8.5f, true, HorzAlign.Center, true);
                    hd.FillColor = Color.FromArgb(255, 213, 128);
                    Teks(db, x, 0, k.Lebar, 6, "[" + tbl + "." + k.Nama + "]", 8.5f, false, k.Rata, true, true);
                    x += k.Lebar;
                }
            }

            // ---- footer ----
            page.PageFooter = new PageFooterBand();
            page.PageFooter.CreateUniqueName();
            page.PageFooter.Height = 8 * MM;
            Teks(page.PageFooter, 0, 1, 130, 6, "Dicetak " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " oleh " + Sesi.Nama + " (" + Sesi.Role + ")", 8);
            Teks(page.PageFooter, 130, 1, 60, 6, "Halaman [Page#] dari [TotalPages#]", 8, false, HorzAlign.Right, false, true);

            rpt.DoublePass = true;   // agar [TotalPages#] terisi
            rpt.Show();
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

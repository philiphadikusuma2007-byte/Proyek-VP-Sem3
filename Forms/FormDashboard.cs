using System.Data;

namespace SuryaMandiriPrint;

/// <summary>Ringkasan aktivitas sesuai hak akses role.</summary>
public class FormDashboard : HalamanForm
{
    readonly FlowLayoutPanel kartu = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
    readonly DataGridView grid = UI.Grid();
    readonly Label judulGrid = new Label { Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 20000 };
    readonly List<(Label label, string sql, Func<object, string> fmt)> daftar = new();

    public FormDashboard() : base("Dashboard")
    {
        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        var judul = UI.Judul("Dashboard - " + Sesi.Role);

        wrap.Controls.Add(grid);
        wrap.Controls.Add(judulGrid);
        wrap.Controls.Add(kartu);
        wrap.Controls.Add(judul);
        Controls.Add(wrap);

        Func<object, string> n = v => UI.Angka(v);
        Func<object, string> rp = v => UI.Rp(v);

        switch (Sesi.Role)
        {
            case RoleNama.Depan:
                Kartu("Pesanan Hari Ini", "SELECT COUNT(*) FROM nota WHERE DATE(tanggal)=CURDATE()", n, UI.Oranye);
                Kartu("Menunggu", "SELECT COUNT(*) FROM nota WHERE status_pesanan='Menunggu'", n, Color.Goldenrod);
                Kartu("Sedang Diproses", "SELECT COUNT(*) FROM nota WHERE status_pesanan='Diproses'", n, UI.Biru);
                Kartu("Tertunda", "SELECT COUNT(*) FROM nota WHERE status_pesanan='Tertunda'", n, UI.Merah);
                Kartu("Selesai Hari Ini", "SELECT COUNT(*) FROM nota WHERE status_pesanan='Selesai' AND DATE(tanggal)=CURDATE()", n, UI.Hijau);
                judulGrid.Text = "Pesanan Terbaru";
                sqlGrid = "SELECT no_nota, tanggal, nama_pelanggan, jumlah_file, total_print, status_pesanan FROM nota ORDER BY id_nota DESC LIMIT 20";
                break;

            case RoleNama.Belakang:
                Kartu("Antrian Produksi", "SELECT COUNT(*) FROM nota WHERE status_pesanan IN ('Menunggu','Diproses','Tertunda')", n, UI.Oranye);
                Kartu("Lembar Dicetak Hari Ini",
                      "SELECT IFNULL(SUM(d.total_lembar),0) FROM detail_cetak d JOIN nota n ON n.id_nota=d.id_nota " +
                      "WHERE n.status_pesanan IN ('Diproses','Selesai') AND d.tanggal_cetak=CURDATE()", n, UI.Biru);
                Kartu("Kertas Menipis", "SELECT COUNT(*) FROM kertas WHERE aktif=1 AND stok<=stok_minimum", n, UI.Merah);
                Kartu("Tinta Menipis", "SELECT COUNT(*) FROM tinta WHERE stok<=stok_minimum", n, UI.Merah);
                Kartu("Counter Tinta (lembar)",
                      "SELECT CONCAT(FORMAT((SELECT CAST(nilai AS UNSIGNED) FROM pengaturan WHERE kunci='counter_lembar_tinta'),0), ' / ', " +
                      "FORMAT((SELECT CAST(nilai AS UNSIGNED) FROM pengaturan WHERE kunci='ambang_lembar_tinta'),0))",
                      v => Convert.ToString(v), UI.Hijau);
                judulGrid.Text = "Stok Kertas Menipis";
                sqlGrid = "SELECT nama_kertas, satuan, stok, stok_minimum FROM kertas WHERE aktif=1 AND stok<=stok_minimum ORDER BY stok";
                break;

            case RoleNama.Kasir:
                Kartu("Penjualan Hari Ini", "SELECT IFNULL(SUM(total_bayar),0) FROM penjualan WHERE DATE(tanggal_transaksi)=CURDATE()", rp, UI.Oranye);
                Kartu("Transaksi Hari Ini", "SELECT COUNT(*) FROM penjualan WHERE DATE(tanggal_transaksi)=CURDATE()", n, UI.Biru);
                Kartu("Belum Lunas", "SELECT IFNULL(SUM(total_bayar),0) FROM penjualan WHERE status_pembayaran='Belum Lunas'", rp, UI.Merah);
                Kartu("Siap Dibayar",
                      "SELECT COUNT(*) FROM nota n LEFT JOIN penjualan p ON p.id_nota=n.id_nota " +
                      "WHERE n.status_pesanan='Selesai' AND p.id_penjualan IS NULL", n, UI.Hijau);
                judulGrid.Text = "Pesanan Selesai - Menunggu Pembayaran";
                sqlGrid = "SELECT n.no_nota, n.tanggal, n.nama_pelanggan, n.total_print, n.total_harga FROM nota n " +
                          "LEFT JOIN penjualan p ON p.id_nota=n.id_nota WHERE n.status_pesanan='Selesai' AND p.id_penjualan IS NULL ORDER BY n.id_nota";
                break;
        }

        Load += (s, e) => { Segarkan(); timer.Start(); };
        timer.Tick += (s, e) => Segarkan();
    }

    string sqlGrid;

    void Kartu(string judul, string sql, Func<object, string> fmt, Color aksen)
    {
        kartu.Controls.Add(UI.Kartu(judul, out Label nilai, aksen));
        daftar.Add((nilai, sql, fmt));
    }

    void Segarkan()
    {
        try
        {
            foreach (var (label, sql, fmt) in daftar)
                label.Text = fmt(Db.Nilai(sql));

            grid.DataSource = Db.Tabel(sqlGrid);
            UI.Kol(grid, "no_nota", "No. Nota");
            UI.Kol(grid, "tanggal", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(grid, "nama_pelanggan", "Pelanggan", 120);
            UI.Kol(grid, "jumlah_file", "File", 40, false, "N0", true);
            UI.Kol(grid, "total_print", "Total Lembar", 60, false, "N0", true);
            UI.Kol(grid, "total_harga", "Total", 80, true);
            UI.Kol(grid, "status_pesanan", "Status", 70);
            UI.Kol(grid, "nama_kertas", "Bahan", 150);
            UI.Kol(grid, "satuan", "Satuan", 50);
            UI.Kol(grid, "stok", "Stok", 50, false, "N0", true);
            UI.Kol(grid, "stok_minimum", "Stok Min.", 50, false, "N0", true);
        }
        catch (Exception ex) { timer.Stop(); UI.Error(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

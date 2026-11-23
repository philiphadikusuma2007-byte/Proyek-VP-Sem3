using System.Data;
using SeriesChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType;

namespace SuryaMandiriPrint;

/// <summary>
/// KASIR - Sales Report: penjualan harian/mingguan/bulanan, rekap pendapatan, rincian per nota,
/// rekap metode pembayaran, grafik perkembangan penjualan, filter tanggal, cetak (FastReport) & export.
/// </summary>
public class FormSalesReport : HalamanForm
{
    readonly DateTimePicker dtDari = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short };
    readonly DateTimePicker dtSampai = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short };
    readonly ComboBox cmbPeriode = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Panel panelGrafik = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly DataGridView gPeriode = UI.Grid(), gRincian = UI.Grid(), gMetode = UI.Grid();
    readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
    readonly Label kHari, kMinggu, kBulan, kTotal, kTerbayar, kPiutang;

    DataTable tPeriode, tRincian, tMetode;
    decimal sumTotal, sumTerbayar, sumPiutang;
    int sumTransaksi;

    public FormSalesReport() : base("Sales Report")
    {
        Sesi.Wajib(RoleNama.Kasir);

        cmbPeriode.Items.AddRange(PeriodeHelper.Nama);
        cmbPeriode.SelectedIndex = 0;
        dtDari.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        dtSampai.Value = DateTime.Today;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UI.Judul("Sales Report (Kasir)"), 0, 0);

        var bar = UI.Baris();
        bar.Controls.Add(UI.Field("Dari tanggal", dtDari));
        bar.Controls.Add(UI.Field("Sampai tanggal", dtSampai));
        bar.Controls.Add(UI.Field("Kelompokkan", cmbPeriode));
        bar.Controls.Add(UI.Tombol("Tampilkan", (s, e) => Muat(), UI.Biru, 100));
        bar.Controls.Add(UI.Tombol("Cetak / Preview", (s, e) => Cetak(), null, 130));
        bar.Controls.Add(UI.Tombol("Export CSV", (s, e) => ExportCsv(), UI.Gelap, 110));
        root.Controls.Add(bar, 0, 1);

        var cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        cards.Controls.Add(UI.Kartu("Penjualan Hari Ini", out kHari, UI.Oranye));
        cards.Controls.Add(UI.Kartu("Penjualan Minggu Ini", out kMinggu, UI.Biru));
        cards.Controls.Add(UI.Kartu("Penjualan Bulan Ini", out kBulan, UI.Hijau));
        cards.Controls.Add(UI.Kartu("Total Periode Terpilih", out kTotal, UI.Gelap));
        cards.Controls.Add(UI.Kartu("Terbayar (Lunas)", out kTerbayar, UI.Hijau));
        cards.Controls.Add(UI.Kartu("Belum Lunas (Piutang)", out kPiutang, UI.Merah));
        root.Controls.Add(cards, 0, 2);

        var tengah = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        tengah.Controls.Add(panelGrafik, 0, 0);

        TabPage Tab(string judul, DataGridView g)
        {
            var tp = new TabPage(judul) { Padding = new Padding(4) };
            tp.Controls.Add(g);
            return tp;
        }
        tabs.TabPages.Add(Tab("Rekap per Periode & Pendapatan", gPeriode));
        tabs.TabPages.Add(Tab("Rincian Transaksi per Nota", gRincian));
        tabs.TabPages.Add(Tab("Rekap Metode Pembayaran", gMetode));
        tengah.Controls.Add(tabs, 0, 1);
        root.Controls.Add(tengah, 0, 3);
        Controls.Add(root);

        UI.WarnaStatus(gRincian, "status_pembayaran");
        Load += (s, e) => Muat();
    }

    decimal Jumlah(string kondisi) =>
        Convert.ToDecimal(Db.Nilai("SELECT IFNULL(SUM(total_bayar),0) FROM penjualan WHERE " + kondisi));

    void Muat()
    {
        try
        {
            int jenis = cmbPeriode.SelectedIndex;
            DateTime a = dtDari.Value.Date, b = dtSampai.Value.Date.AddDays(1);
            if (b <= a) { UI.Info("Tanggal akhir harus sama atau setelah tanggal awal."); return; }

            // ---- kartu hari / minggu / bulan ini ----
            kHari.Text = UI.Rp(Jumlah("DATE(tanggal_transaksi) = CURDATE()"));
            kMinggu.Text = UI.Rp(Jumlah("YEARWEEK(tanggal_transaksi, 1) = YEARWEEK(CURDATE(), 1)"));
            kBulan.Text = UI.Rp(Jumlah("YEAR(tanggal_transaksi) = YEAR(CURDATE()) AND MONTH(tanggal_transaksi) = MONTH(CURDATE())"));

            // ---- rekap per periode ----
            string kunci = PeriodeHelper.KunciSql(jenis, "p.tanggal_transaksi");
            var src = Db.Tabel(
                "SELECT DATE_FORMAT(" + kunci + ", '%Y-%m-%d') AS kunci, COUNT(*) AS transaksi, SUM(p.total_bayar) AS total, " +
                "SUM(CASE WHEN p.status_pembayaran = 'Lunas' THEN p.total_bayar ELSE 0 END) AS terbayar " +
                "FROM penjualan p WHERE p.tanggal_transaksi >= @a AND p.tanggal_transaksi < @b GROUP BY kunci ORDER BY kunci",
                ("@a", a), ("@b", b));

            tPeriode = new DataTable("periode");
            tPeriode.Columns.Add("periode", typeof(string));
            tPeriode.Columns.Add("transaksi", typeof(int));
            tPeriode.Columns.Add("total", typeof(decimal));
            tPeriode.Columns.Add("terbayar", typeof(decimal));
            tPeriode.Columns.Add("piutang", typeof(decimal));

            var data = new List<(string label, double nilai)>();
            sumTotal = sumTerbayar = sumPiutang = 0;
            sumTransaksi = 0;
            foreach (DataRow r in src.Rows)
            {
                string label = PeriodeHelper.Label(jenis, Convert.ToString(r["kunci"]));
                int trx = UI.Int(r["transaksi"]);
                decimal total = Convert.ToDecimal(r["total"]), lunas = Convert.ToDecimal(r["terbayar"]);
                tPeriode.Rows.Add(label, trx, total, lunas, total - lunas);
                data.Add((label, (double)total));
                sumTransaksi += trx; sumTotal += total; sumTerbayar += lunas; sumPiutang += total - lunas;
            }
            kTotal.Text = UI.Rp(sumTotal);
            kTerbayar.Text = UI.Rp(sumTerbayar);
            kPiutang.Text = UI.Rp(sumPiutang);

            gPeriode.DataSource = tPeriode;
            UI.Kol(gPeriode, "periode", "Periode", 120);
            UI.Kol(gPeriode, "transaksi", "Jumlah Transaksi", 60, false, "N0", true);
            UI.Kol(gPeriode, "total", "Total Penjualan (Rp)", 90, true);
            UI.Kol(gPeriode, "terbayar", "Terbayar / Pendapatan (Rp)", 90, true);
            UI.Kol(gPeriode, "piutang", "Belum Lunas (Rp)", 90, true);

            // ---- rincian per nota ----
            tRincian = Db.Tabel(
                "SELECT n.no_nota, p.tanggal_transaksi, n.nama_pelanggan, p.metode_pembayaran, p.status_pembayaran, p.total_bayar " +
                "FROM penjualan p JOIN nota n ON n.id_nota = p.id_nota " +
                "WHERE p.tanggal_transaksi >= @a AND p.tanggal_transaksi < @b ORDER BY p.tanggal_transaksi",
                ("@a", a), ("@b", b));
            gRincian.DataSource = tRincian;
            UI.Kol(gRincian, "no_nota", "No. Nota", 90);
            UI.Kol(gRincian, "tanggal_transaksi", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(gRincian, "nama_pelanggan", "Pelanggan", 140);
            UI.Kol(gRincian, "metode_pembayaran", "Metode", 70);
            UI.Kol(gRincian, "status_pembayaran", "Status Bayar", 80);
            UI.Kol(gRincian, "total_bayar", "Total (Rp)", 90, true);

            // ---- rekap metode pembayaran ----
            tMetode = Db.Tabel(
                "SELECT p.metode_pembayaran AS metode, COUNT(*) AS transaksi, SUM(p.total_bayar) AS total " +
                "FROM penjualan p WHERE p.tanggal_transaksi >= @a AND p.tanggal_transaksi < @b " +
                "GROUP BY p.metode_pembayaran ORDER BY total DESC",
                ("@a", a), ("@b", b));
            gMetode.DataSource = tMetode;
            UI.Kol(gMetode, "metode", "Metode Pembayaran", 150);
            UI.Kol(gMetode, "transaksi", "Jumlah Transaksi", 80, false, "N0", true);
            UI.Kol(gMetode, "total", "Total (Rp)", 120, true);

            // ---- grafik ----
            foreach (var c in panelGrafik.Controls.Cast<Control>().ToList()) c.Dispose();
            panelGrafik.Controls.Clear();
            panelGrafik.Controls.Add(ChartHelper.Buat(
                "Grafik Perkembangan Penjualan (" + PeriodeHelper.Nama[jenis] + ")", "Total penjualan (Rp)",
                SeriesChartType.Column, data, UI.Oranye, "Rupiah"));
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void Cetak()
    {
        if (tPeriode == null) Muat();
        if (tPeriode == null || tPeriode.Rows.Count == 0) { UI.Info("Tidak ada data penjualan pada rentang tanggal tersebut."); return; }

        int jenis = cmbPeriode.SelectedIndex;
        var data = tPeriode.Rows.Cast<DataRow>()
                           .Select(r => (Convert.ToString(r["periode"]), Convert.ToDouble(r["total"]))).ToList();
        Image grafik = ChartHelper.KeGambar(ChartHelper.Buat(
            "Grafik Perkembangan Penjualan (" + PeriodeHelper.Nama[jenis] + ")", "Total penjualan (Rp)",
            SeriesChartType.Column, data, UI.Oranye, "Rupiah"), 1000, 400);

        Func<object, string> rp = v => UI.Rp(v);
        Func<object, string> angka = v => UI.Angka(v);
        Func<object, string> tgl = v => ((DateTime)v).ToString("dd/MM/yyyy HH:mm");

        var bagian = new List<BagianLaporan>
        {
            new BagianLaporan
            {
                Judul = "Rekap Penjualan & Pendapatan per Periode (" + PeriodeHelper.Nama[jenis] + ")",
                Tabel = tPeriode,
                Kolom =
                {
                    new KolomLaporan("periode", "Periode", 50),
                    new KolomLaporan("transaksi", "Transaksi", 22, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("total", "Total Penjualan", 40, FastReport.HorzAlign.Right, rp),
                    new KolomLaporan("terbayar", "Terbayar", 40, FastReport.HorzAlign.Right, rp),
                    new KolomLaporan("piutang", "Belum Lunas", 38, FastReport.HorzAlign.Right, rp)
                }
            },
            new BagianLaporan
            {
                Judul = "Rekap Metode Pembayaran",
                Tabel = tMetode,
                Kolom =
                {
                    new KolomLaporan("metode", "Metode", 70),
                    new KolomLaporan("transaksi", "Transaksi", 50, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("total", "Total", 70, FastReport.HorzAlign.Right, rp)
                }
            },
            new BagianLaporan
            {
                Judul = "Rincian Transaksi per Nomor Nota",
                Tabel = tRincian,
                Kolom =
                {
                    new KolomLaporan("no_nota", "No. Nota", 32),
                    new KolomLaporan("tanggal_transaksi", "Tanggal", 33, FastReport.HorzAlign.Center, tgl),
                    new KolomLaporan("nama_pelanggan", "Pelanggan", 45),
                    new KolomLaporan("metode_pembayaran", "Metode", 20),
                    new KolomLaporan("status_pembayaran", "Status", 22),
                    new KolomLaporan("total_bayar", "Total", 38, FastReport.HorzAlign.Right, rp)
                }
            }
        };

        var ringkasan = new List<string>
        {
            "Jumlah transaksi         : " + UI.Angka(sumTransaksi),
            "Total penjualan          : " + UI.Rp(sumTotal),
            "Pendapatan terbayar      : " + UI.Rp(sumTerbayar),
            "Belum lunas (piutang)    : " + UI.Rp(sumPiutang)
        };

        ReportService.TampilkanLaporan("LAPORAN PENJUALAN",
            "Periode " + dtDari.Value.ToString("dd/MM/yyyy") + " s/d " + dtSampai.Value.ToString("dd/MM/yyyy"),
            ringkasan, grafik, bagian);
    }

    void ExportCsv()
    {
        var dt = tabs.SelectedIndex switch { 0 => tPeriode, 1 => tRincian, _ => tMetode };
        UI.SimpanCsv(dt, "sales_report_" + DateTime.Today.ToString("yyyyMMdd"));
    }
}

using System.Data;
using SeriesChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType;

namespace SuryaMandiriPrint;

/// <summary>
/// ADMIN BELAKANG - Print Volume Report: total lembar dicetak harian/mingguan/bulanan, rekap per jenis bahan,
/// GRAFIK perkembangan volume cetak, perbandingan antarperiode, filter tanggal, cetak (FastReport) & export.
/// Volume dihitung dari detail_cetak pesanan berstatus Diproses/Selesai (bahan kategori lembar).
/// </summary>
public class FormPrintVolume : HalamanForm
{
    const string DasarFrom =
        "FROM detail_cetak d JOIN nota n ON n.id_nota = d.id_nota JOIN kertas k ON k.id_kertas = d.id_kertas " +
        "WHERE n.status_pesanan IN ('Diproses','Selesai') AND k.kategori = 'LEMBAR' ";

    readonly DateTimePicker dtDari = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short };
    readonly DateTimePicker dtSampai = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short };
    readonly ComboBox cmbPeriode = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Panel panelGrafik = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly DataGridView gPeriode = UI.Grid(), gJenis = UI.Grid();
    readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
    readonly Label kHari, kMinggu, kBulan, kTotal, kBanding;

    DataTable tPeriode, tJenis;
    long sumLembar, sumPesanan, sumFile, lembarSebelumnya;

    public FormPrintVolume() : base("Print Volume Report")
    {
        Sesi.Wajib(RoleNama.Belakang);

        cmbPeriode.Items.AddRange(PeriodeHelper.Nama);
        cmbPeriode.SelectedIndex = 0;
        dtDari.Value = DateTime.Today.AddDays(-29);
        dtSampai.Value = DateTime.Today;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UI.Judul("Print Volume Report (Back Office)"), 0, 0);

        var bar = UI.Baris();
        bar.Controls.Add(UI.Field("Dari tanggal", dtDari));
        bar.Controls.Add(UI.Field("Sampai tanggal", dtSampai));
        bar.Controls.Add(UI.Field("Kelompokkan", cmbPeriode));
        bar.Controls.Add(UI.Tombol("Tampilkan", (s, e) => Muat(), UI.Biru, 100));
        bar.Controls.Add(UI.Tombol("Cetak / Preview", (s, e) => Cetak(), null, 130));
        bar.Controls.Add(UI.Tombol("Export CSV", (s, e) => ExportCsv(), UI.Gelap, 110));
        root.Controls.Add(bar, 0, 1);

        var cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        cards.Controls.Add(UI.Kartu("Total Print Hari Ini (lembar)", out kHari, UI.Oranye));
        cards.Controls.Add(UI.Kartu("Total Print Minggu Ini", out kMinggu, UI.Biru));
        cards.Controls.Add(UI.Kartu("Total Print Bulan Ini", out kBulan, UI.Hijau));
        cards.Controls.Add(UI.Kartu("Total Periode Terpilih", out kTotal, UI.Gelap));
        cards.Controls.Add(UI.Kartu("Vs Periode Sebelumnya", out kBanding, UI.Merah));
        root.Controls.Add(cards, 0, 2);

        var tengah = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        tengah.Controls.Add(panelGrafik, 0, 0);

        TabPage Tab(string judul, DataGridView g)
        {
            var tp = new TabPage(judul) { Padding = new Padding(4) };
            tp.Controls.Add(g);
            return tp;
        }
        tabs.TabPages.Add(Tab("Volume per Periode & Perbandingan", gPeriode));
        tabs.TabPages.Add(Tab("Rekap per Jenis Bahan / Pesanan", gJenis));
        tengah.Controls.Add(tabs, 0, 1);
        root.Controls.Add(tengah, 0, 3);
        Controls.Add(root);

        Load += (s, e) => Muat();
    }

    long Volume(string kondisi) =>
        Convert.ToInt64(Db.Nilai("SELECT IFNULL(SUM(d.total_lembar), 0) " + DasarFrom + " AND " + kondisi));

    void Muat()
    {
        try
        {
            int jenis = cmbPeriode.SelectedIndex;
            DateTime a = dtDari.Value.Date, b = dtSampai.Value.Date;
            if (b < a) { UI.Info("Tanggal akhir harus sama atau setelah tanggal awal."); return; }

            // ---- kartu hari / minggu / bulan ini ----
            kHari.Text = UI.Angka(Volume("d.tanggal_cetak = CURDATE()"));
            kMinggu.Text = UI.Angka(Volume("YEARWEEK(d.tanggal_cetak, 1) = YEARWEEK(CURDATE(), 1)"));
            kBulan.Text = UI.Angka(Volume("YEAR(d.tanggal_cetak) = YEAR(CURDATE()) AND MONTH(d.tanggal_cetak) = MONTH(CURDATE())"));

            // ---- volume per periode ----
            string kunci = PeriodeHelper.KunciSql(jenis, "d.tanggal_cetak");
            var src = Db.Tabel(
                "SELECT DATE_FORMAT(" + kunci + ", '%Y-%m-%d') AS kunci, COUNT(DISTINCT d.id_nota) AS pesanan, " +
                "COUNT(*) AS jumlah_file, SUM(d.total_lembar) AS lembar " + DasarFrom +
                "AND d.tanggal_cetak BETWEEN @a AND @b GROUP BY kunci ORDER BY kunci",
                ("@a", a), ("@b", b));

            tPeriode = new DataTable("periode");
            tPeriode.Columns.Add("periode", typeof(string));
            tPeriode.Columns.Add("pesanan", typeof(int));
            tPeriode.Columns.Add("jumlah_file", typeof(int));
            tPeriode.Columns.Add("lembar", typeof(long));
            tPeriode.Columns.Add("selisih", typeof(string));      // dibanding periode sebelumnya
            tPeriode.Columns.Add("persen", typeof(string));

            var data = new List<(string label, double nilai)>();
            sumLembar = sumPesanan = sumFile = 0;
            long? sebelum = null;
            foreach (DataRow r in src.Rows)
            {
                string label = PeriodeHelper.Label(jenis, Convert.ToString(r["kunci"]));
                long lembar = Convert.ToInt64(r["lembar"]);
                string selisih = "-", persen = "-";
                if (sebelum.HasValue)
                {
                    long d = lembar - sebelum.Value;
                    selisih = (d >= 0 ? "+" : "") + d.ToString("N0", UI.ID);
                    persen = sebelum.Value == 0 ? "-" : ((d * 100.0 / sebelum.Value) >= 0 ? "+" : "") +
                             (d * 100.0 / sebelum.Value).ToString("0.0", UI.ID) + "%";
                }
                tPeriode.Rows.Add(label, UI.Int(r["pesanan"]), UI.Int(r["jumlah_file"]), lembar, selisih, persen);
                data.Add((label, lembar));
                sebelum = lembar;
                sumLembar += lembar; sumPesanan += UI.Int(r["pesanan"]); sumFile += UI.Int(r["jumlah_file"]);
            }
            kTotal.Text = UI.Angka(sumLembar);

            // ---- perbandingan dengan rentang sebelumnya yang sama panjang ----
            int hari = (b - a).Days + 1;
            lembarSebelumnya = Convert.ToInt64(Db.Nilai(
                "SELECT IFNULL(SUM(d.total_lembar), 0) " + DasarFrom + "AND d.tanggal_cetak BETWEEN @a AND @b",
                ("@a", a.AddDays(-hari)), ("@b", a.AddDays(-1))));
            if (lembarSebelumnya == 0) kBanding.Text = sumLembar == 0 ? "-" : "Baru (tanpa pembanding)";
            else
            {
                double pct = (sumLembar - lembarSebelumnya) * 100.0 / lembarSebelumnya;
                kBanding.Text = (pct >= 0 ? "+" : "") + pct.ToString("0.0", UI.ID) + "%  (" + UI.Angka(lembarSebelumnya) + ")";
                kBanding.ForeColor = pct >= 0 ? UI.Hijau : UI.Merah;
            }

            gPeriode.DataSource = tPeriode;
            UI.Kol(gPeriode, "periode", "Periode", 130);
            UI.Kol(gPeriode, "pesanan", "Jumlah Pesanan", 60, false, "N0", true);
            UI.Kol(gPeriode, "jumlah_file", "Jumlah File", 60, false, "N0", true);
            UI.Kol(gPeriode, "lembar", "Total Lembar Dicetak", 80, false, "N0", true);
            UI.Kol(gPeriode, "selisih", "Selisih vs Periode Sebelumnya", 90, false, null, true);
            UI.Kol(gPeriode, "persen", "Perubahan (%)", 60, false, null, true);

            // ---- rekap per jenis bahan ----
            tJenis = Db.Tabel(
                "SELECT k.nama_kertas AS bahan, CASE d.sisi WHEN 1 THEN '1 Halaman' ELSE 'Bolak-balik' END AS sisi_cetak, " +
                "COUNT(*) AS jumlah_file, SUM(d.total_lembar) AS lembar " + DasarFrom +
                "AND d.tanggal_cetak BETWEEN @a AND @b GROUP BY k.id_kertas, k.nama_kertas, d.sisi ORDER BY lembar DESC",
                ("@a", a), ("@b", b));
            gJenis.DataSource = tJenis;
            UI.Kol(gJenis, "bahan", "Jenis Bahan", 180);
            UI.Kol(gJenis, "sisi_cetak", "Sisi", 80);
            UI.Kol(gJenis, "jumlah_file", "Jumlah File", 70, false, "N0", true);
            UI.Kol(gJenis, "lembar", "Total Lembar", 90, false, "N0", true);

            // ---- GRAFIK perkembangan volume cetak ----
            foreach (var c in panelGrafik.Controls.Cast<Control>().ToList()) c.Dispose();
            panelGrafik.Controls.Clear();
            panelGrafik.Controls.Add(ChartHelper.Buat(
                "Grafik Perkembangan Volume Cetak (" + PeriodeHelper.Nama[jenis] + ")", "Lembar dicetak",
                SeriesChartType.Column, data, UI.Biru, "Lembar"));
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void Cetak()
    {
        if (tPeriode == null) Muat();
        if (tPeriode == null || tPeriode.Rows.Count == 0) { UI.Info("Tidak ada data volume cetak pada rentang tanggal tersebut."); return; }

        int jenis = cmbPeriode.SelectedIndex;
        var data = tPeriode.Rows.Cast<DataRow>()
                           .Select(r => (Convert.ToString(r["periode"]), Convert.ToDouble(r["lembar"]))).ToList();
        Image grafik = ChartHelper.KeGambar(ChartHelper.Buat(
            "Grafik Perkembangan Volume Cetak (" + PeriodeHelper.Nama[jenis] + ")", "Lembar dicetak",
            SeriesChartType.Column, data, UI.Biru, "Lembar"), 1000, 400);

        Func<object, string> angka = v => UI.Angka(v);

        var bagian = new List<BagianLaporan>
        {
            new BagianLaporan
            {
                Judul = "Volume Cetak per Periode (" + PeriodeHelper.Nama[jenis] + ") & Perbandingan Antarperiode",
                Tabel = tPeriode,
                Kolom =
                {
                    new KolomLaporan("periode", "Periode", 52),
                    new KolomLaporan("pesanan", "Pesanan", 22, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("jumlah_file", "File", 20, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("lembar", "Total Lembar", 32, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("selisih", "Selisih", 33, FastReport.HorzAlign.Right),
                    new KolomLaporan("persen", "Perubahan", 31, FastReport.HorzAlign.Right)
                }
            },
            new BagianLaporan
            {
                Judul = "Rekap Volume Cetak berdasarkan Jenis Pesanan (Bahan & Sisi)",
                Tabel = tJenis,
                Kolom =
                {
                    new KolomLaporan("bahan", "Jenis Bahan", 85),
                    new KolomLaporan("sisi_cetak", "Sisi", 35),
                    new KolomLaporan("jumlah_file", "Jumlah File", 30, FastReport.HorzAlign.Right, angka),
                    new KolomLaporan("lembar", "Total Lembar", 40, FastReport.HorzAlign.Right, angka)
                }
            }
        };

        var ringkasan = new List<string>
        {
            "Total lembar dicetak      : " + UI.Angka(sumLembar),
            "Jumlah pesanan / file     : " + UI.Angka(sumPesanan) + " / " + UI.Angka(sumFile),
            "Periode sebelumnya (lembar): " + UI.Angka(lembarSebelumnya) + "   [" + kBanding.Text + "]"
        };

        ReportService.TampilkanLaporan("LAPORAN VOLUME CETAK",
            "Periode " + dtDari.Value.ToString("dd/MM/yyyy") + " s/d " + dtSampai.Value.ToString("dd/MM/yyyy"),
            ringkasan, grafik, bagian);
    }

    void ExportCsv()
    {
        var dt = tabs.SelectedIndex == 0 ? tPeriode : tJenis;
        UI.SimpanCsv(dt, "print_volume_" + DateTime.Today.ToString("yyyyMMdd"));
    }
}

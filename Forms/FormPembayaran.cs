using System.Data;

namespace SuryaMandiriPrint;

/// <summary>
/// KASIR - Form Pembayaran: memproses pembayaran pesanan berstatus Selesai, membuat nota (FastReport),
/// riwayat transaksi, pelunasan tempo, dan memantau status pembayaran.
/// Validasi total & kembalian dilakukan juga oleh trigger MySQL (trg_penjualan_bi / trg_penjualan_bu).
/// </summary>
public class FormPembayaran : HalamanForm
{
    // ---- tab pembayaran ----
    readonly DataGridView gSiap = UI.Grid(), gDetail = UI.Grid();
    readonly Label lblNota = new Label { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
    readonly Label lblTotal = new Label { AutoSize = true, Font = new Font("Segoe UI", 20f, FontStyle.Bold), ForeColor = UI.Oranye, Text = "Rp0" };
    readonly Label lblKembali = new Label { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = UI.Hijau, Text = "Rp0" };
    readonly ComboBox cmbMetode = new ComboBox { Width = 290, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown numUang = new NumericUpDown { Width = 290, Minimum = 0, Maximum = 2000000000, ThousandsSeparator = true, Increment = 1000 };
    readonly DateTimePicker dtTempo = new DateTimePicker { Width = 290, Format = DateTimePickerFormat.Short };
    readonly Button btnBayar;

    // ---- tab riwayat ----
    readonly DataGridView gRiwayat = UI.Grid();
    readonly ComboBox cmbStatusBayar = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox txtCari = new TextBox { Width = 160 };
    readonly DateTimePicker dtDari = new DateTimePicker { Width = 105, Format = DateTimePickerFormat.Short };
    readonly DateTimePicker dtSampai = new DateTimePicker { Width = 105, Format = DateTimePickerFormat.Short };

    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 15000 };
    bool memuat;
    int totalNota;

    public FormPembayaran() : base("Form Pembayaran")
    {
        Sesi.Wajib(RoleNama.Kasir);

        cmbMetode.Items.AddRange(new object[] { "Tunai", "Transfer", "QRIS", "Tempo" });
        cmbMetode.SelectedIndex = 0;
        cmbStatusBayar.Items.AddRange(new object[] { "Semua", "Lunas", "Belum Lunas" });
        cmbStatusBayar.SelectedIndex = 0;
        dtDari.Value = DateTime.Today.AddDays(-30);
        dtSampai.Value = DateTime.Today;
        dtTempo.Value = DateTime.Today.AddDays(14);

        btnBayar = UI.Tombol("BAYAR && CETAK NOTA", (s, e) => Bayar(), UI.Hijau, 290);
        btnBayar.Margin = new Padding(0, 14, 0, 0);
        btnBayar.Height = 44;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UI.Judul("Form Pembayaran (Kasir)"), 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuatTabBayar());
        tabs.TabPages.Add(BuatTabRiwayat());
        root.Controls.Add(tabs, 0, 1);
        Controls.Add(root);

        UI.WarnaStatus(gRiwayat, "status_pembayaran");
        gSiap.SelectionChanged += (s, e) => PilihNota();
        cmbMetode.SelectedIndexChanged += (s, e) => AturMetode();
        numUang.ValueChanged += (s, e) => HitungKembali();
        gRiwayat.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) CetakUlang(); };

        Load += (s, e) => { MuatSiap(); MuatRiwayat(); AturMetode(); timer.Start(); };
        timer.Tick += (s, e) => { if (!memuat) MuatSiap(); };
    }

    // ================================================================= UI
    TabPage BuatTabBayar()
    {
        var tp = new TabPage("Pembayaran Baru") { Padding = new Padding(8), BackColor = UI.Latar };

        var kanan = new Panel { Dock = DockStyle.Right, Width = 330, Padding = new Padding(14), BackColor = Color.White };
        var f = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        f.Controls.Add(new Label { Text = "Pesanan dipilih", AutoSize = true, ForeColor = Color.DimGray });
        f.Controls.Add(lblNota);
        f.Controls.Add(new Label { Text = "TOTAL TAGIHAN", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 14, 3, 0) });
        f.Controls.Add(lblTotal);
        f.Controls.Add(UI.Field("Metode pembayaran", cmbMetode));
        f.Controls.Add(UI.Field("Uang diterima (Tunai)", numUang));
        f.Controls.Add(new Label { Text = "Kembalian", AutoSize = true, ForeColor = Color.DimGray });
        f.Controls.Add(lblKembali);
        f.Controls.Add(UI.Field("Jatuh tempo (khusus Tempo)", dtTempo));
        f.Controls.Add(btnBayar);
        kanan.Controls.Add(f);

        var kiri = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        kiri.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        kiri.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var b1 = new GroupBox { Text = "Master: Pesanan Selesai - Menunggu Pembayaran", Dock = DockStyle.Fill };
        b1.Controls.Add(gSiap);
        var b2 = new GroupBox { Text = "Detail: Rincian Pesanan Terpilih", Dock = DockStyle.Fill };
        b2.Controls.Add(gDetail);
        kiri.Controls.Add(b1, 0, 0);
        kiri.Controls.Add(b2, 0, 1);

        tp.Controls.Add(kiri);
        tp.Controls.Add(kanan);
        return tp;
    }

    TabPage BuatTabRiwayat()
    {
        var tp = new TabPage("Riwayat Transaksi & Status Pembayaran") { Padding = new Padding(8), BackColor = UI.Latar };
        var bar = UI.Baris();
        bar.Dock = DockStyle.Top;
        bar.Controls.Add(UI.Field("Status bayar", cmbStatusBayar));
        bar.Controls.Add(UI.Field("Cari no. nota / pelanggan", txtCari));
        bar.Controls.Add(UI.Field("Dari", dtDari));
        bar.Controls.Add(UI.Field("Sampai", dtSampai));
        bar.Controls.Add(UI.Tombol("Tampilkan", (s, e) => MuatRiwayat(), UI.Biru, 100));
        bar.Controls.Add(UI.Tombol("Cetak Ulang Nota", (s, e) => CetakUlang(), null, 140));
        bar.Controls.Add(UI.Tombol("Lunasi (Tempo)", (s, e) => Lunasi(), UI.Hijau, 130));
        tp.Controls.Add(gRiwayat);
        tp.Controls.Add(bar);
        return tp;
    }

    // ================================================================= DATA
    void MuatSiap()
    {
        memuat = true;
        try
        {
            int? pilih = UI.IdTerpilih(gSiap, "id_nota");
            gSiap.DataSource = Db.Tabel(
                "SELECT n.id_nota, n.no_nota, n.tanggal, n.nama_pelanggan, n.jumlah_file, n.total_print, n.total_harga " +
                "FROM nota n LEFT JOIN penjualan p ON p.id_nota = n.id_nota " +
                "WHERE n.status_pesanan = 'Selesai' AND p.id_penjualan IS NULL ORDER BY n.id_nota");
            UI.Sembunyi(gSiap, "id_nota");
            UI.Kol(gSiap, "no_nota", "No. Nota", 90);
            UI.Kol(gSiap, "tanggal", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(gSiap, "nama_pelanggan", "Pelanggan", 140);
            UI.Kol(gSiap, "jumlah_file", "File", 40, false, "N0", true);
            UI.Kol(gSiap, "total_print", "Total Lembar", 70, false, "N0", true);
            UI.Kol(gSiap, "total_harga", "Total (Rp)", 90, true);
            if (pilih.HasValue)
                foreach (DataGridViewRow r in gSiap.Rows)
                    if (UI.Int(r.Cells["id_nota"].Value) == pilih.Value)
                    {
                        gSiap.CurrentCell = r.Cells["no_nota"];
                        r.Selected = true;
                        break;
                    }
        }
        catch (Exception ex) { timer.Stop(); UI.Error(ex); }
        finally { memuat = false; }
        PilihNota();
    }

    void PilihNota()
    {
        if (memuat) return;
        var id = UI.IdTerpilih(gSiap, "id_nota");
        if (id == null)
        {
            lblNota.Text = "(belum ada yang dipilih)";
            totalNota = 0;
            lblTotal.Text = UI.Rp(0);
            gDetail.DataSource = null;
            AturMetode();
            return;
        }
        lblNota.Text = UI.TeksTerpilih(gSiap, "no_nota") + " - " + UI.TeksTerpilih(gSiap, "nama_pelanggan");
        totalNota = UI.IdTerpilih(gSiap, "total_harga") ?? 0;
        lblTotal.Text = UI.Rp(totalNota);
        try
        {
            gDetail.DataSource = Db.Tabel(
                "SELECT d.nama_file, k.nama_kertas, " +
                "CASE WHEN k.kategori <> 'LEMBAR' THEN '-' WHEN d.sisi = 1 THEN '1 Halaman' ELSE 'Bolak-balik' END AS jenis_sisi, " +
                "d.jumlah_halaman, d.jumlah_copy, d.total_lembar, d.harga_satuan, d.subtotal " +
                "FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas WHERE d.id_nota = @n ORDER BY d.id_detail",
                ("@n", id.Value));
            UI.Kol(gDetail, "nama_file", "File", 140); UI.Kol(gDetail, "nama_kertas", "Bahan", 160);
            UI.Kol(gDetail, "jenis_sisi", "Sisi", 70);
            UI.Kol(gDetail, "jumlah_halaman", "Halaman", 55, false, "N0", true);
            UI.Kol(gDetail, "jumlah_copy", "Copy", 45, false, "N0", true);
            UI.Kol(gDetail, "total_lembar", "Total Lembar", 60, false, "N0", true);
            UI.Kol(gDetail, "harga_satuan", "Harga Satuan (Rp)", 80, true);
            UI.Kol(gDetail, "subtotal", "Subtotal (Rp)", 90, true);
        }
        catch (Exception ex) { UI.Error(ex); }
        AturMetode();
    }

    void AturMetode()
    {
        string m = cmbMetode.Text;
        numUang.Enabled = m == "Tunai";
        dtTempo.Enabled = m == "Tempo";
        if (m == "Tunai") numUang.Value = Math.Min(numUang.Maximum, totalNota);
        else numUang.Value = m == "Tempo" ? 0 : Math.Min(numUang.Maximum, totalNota);
        HitungKembali();
    }

    void HitungKembali()
    {
        if (cmbMetode.Text == "Tempo") { lblKembali.Text = "-"; return; }
        lblKembali.Text = UI.Rp((int)numUang.Value - totalNota);
        lblKembali.ForeColor = (int)numUang.Value >= totalNota ? UI.Hijau : UI.Merah;
    }

    void MuatRiwayat()
    {
        try
        {
            string st = cmbStatusBayar.SelectedIndex > 0 ? cmbStatusBayar.Text : "";
            string q = txtCari.Text.Trim();
            gRiwayat.DataSource = Db.Tabel(
                "SELECT p.id_penjualan, n.no_nota, p.tanggal_transaksi, n.nama_pelanggan, p.metode_pembayaran, p.total_bayar, " +
                "p.uang_diterima, p.kembalian, p.status_pembayaran, p.jatuh_tempo " +
                "FROM penjualan p JOIN nota n ON n.id_nota = p.id_nota " +
                "WHERE p.tanggal_transaksi >= @a AND p.tanggal_transaksi < @b " +
                "AND (@s = '' OR p.status_pembayaran = @s) " +
                "AND (@q = '' OR n.no_nota LIKE @lk OR n.nama_pelanggan LIKE @lk) ORDER BY p.id_penjualan DESC",
                ("@a", dtDari.Value.Date), ("@b", dtSampai.Value.Date.AddDays(1)),
                ("@s", st), ("@q", q), ("@lk", "%" + q + "%"));
            UI.Sembunyi(gRiwayat, "id_penjualan");
            UI.Kol(gRiwayat, "no_nota", "No. Nota", 90);
            UI.Kol(gRiwayat, "tanggal_transaksi", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(gRiwayat, "nama_pelanggan", "Pelanggan", 130);
            UI.Kol(gRiwayat, "metode_pembayaran", "Metode", 70);
            UI.Kol(gRiwayat, "total_bayar", "Total (Rp)", 80, true);
            UI.Kol(gRiwayat, "uang_diterima", "Diterima (Rp)", 80, true);
            UI.Kol(gRiwayat, "kembalian", "Kembalian (Rp)", 80, true);
            UI.Kol(gRiwayat, "status_pembayaran", "Status Bayar", 80);
            UI.Kol(gRiwayat, "jatuh_tempo", "Jatuh Tempo", 80, false, "dd/MM/yyyy");
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    // ================================================================= AKSI
    void Bayar()
    {
        try
        {
            Sesi.Wajib(RoleNama.Kasir);
            var id = UI.IdTerpilih(gSiap, "id_nota");
            if (id == null) { UI.Info("Pilih pesanan yang akan dibayar."); return; }

            string metode = cmbMetode.Text;
            if (metode == "Tunai" && (int)numUang.Value < totalNota) { UI.Info("Uang diterima kurang dari total tagihan."); return; }
            if (metode == "Tempo" && dtTempo.Value.Date <= DateTime.Today && !UI.Tanya("Jatuh tempo hari ini / sudah lewat. Lanjutkan?")) return;

            int idNota = id.Value;
            long idPenjualan = 0;
            Db.Transaksi((c, t) =>
            {
                idPenjualan = Db.Sisip(c, t,
                    "INSERT INTO penjualan (id_nota, tanggal_transaksi, metode_pembayaran, uang_diterima, jatuh_tempo, id_user) " +
                    "VALUES (@n, NOW(), @m, @u, @j, @us)",
                    ("@n", idNota), ("@m", metode), ("@u", (int)numUang.Value),
                    ("@j", metode == "Tempo" ? dtTempo.Value.Date : (DateTime?)null), ("@us", Sesi.IdUser));
            });

            MuatSiap();
            MuatRiwayat();
            ReportService.TampilkanNota((int)idPenjualan);
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void CetakUlang()
    {
        var id = UI.IdTerpilih(gRiwayat, "id_penjualan");
        if (id == null) { UI.Info("Pilih transaksi pada tabel riwayat."); return; }
        ReportService.TampilkanNota(id.Value);
    }

    void Lunasi()
    {
        try
        {
            Sesi.Wajib(RoleNama.Kasir);
            var id = UI.IdTerpilih(gRiwayat, "id_penjualan");
            if (id == null) { UI.Info("Pilih transaksi tempo yang akan dilunasi."); return; }
            if (UI.TeksTerpilih(gRiwayat, "status_pembayaran") != "Belum Lunas") { UI.Info("Transaksi ini sudah Lunas."); return; }
            int total = UI.IdTerpilih(gRiwayat, "total_bayar") ?? 0;

            string metode; int uang;
            using (var f = new Form
            {
                Text = "Pelunasan - " + UI.TeksTerpilih(gRiwayat, "no_nota"),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(340, 210),
                MaximizeBox = false, MinimizeBox = false,
                Font = new Font("Segoe UI", 9.5f)
            })
            {
                var m = new ComboBox { Left = 16, Top = 58, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
                m.Items.AddRange(new object[] { "Tunai", "Transfer", "QRIS" });
                m.SelectedIndex = 0;
                var u = new NumericUpDown { Left = 16, Top = 112, Width = 300, Maximum = 2000000000, ThousandsSeparator = true, Value = total };
                m.SelectedIndexChanged += (s, e) => u.Enabled = m.Text == "Tunai";
                var ok = new Button { Text = "Lunasi", Left = 140, Top = 160, Width = 84, DialogResult = DialogResult.OK };
                var batal = new Button { Text = "Batal", Left = 232, Top = 160, Width = 84, DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[]
                {
                    new Label { Text = "Total tagihan: " + UI.Rp(total), Left = 16, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) },
                    new Label { Text = "Metode pelunasan", Left = 16, Top = 38, AutoSize = true },
                    m,
                    new Label { Text = "Uang diterima (Tunai)", Left = 16, Top = 92, AutoSize = true },
                    u, ok, batal
                });
                f.AcceptButton = ok; f.CancelButton = batal;
                if (f.ShowDialog(this) != DialogResult.OK) return;
                metode = m.Text; uang = (int)u.Value;
            }

            int idP = id.Value;
            Db.Transaksi((c, t) =>
                Db.Jalankan(c, t,
                    "UPDATE penjualan SET status_pembayaran='Lunas', metode_pembayaran=@m, uang_diterima=@u, id_user=@us WHERE id_penjualan=@id",
                    ("@m", metode), ("@u", uang), ("@us", Sesi.IdUser), ("@id", idP)));

            MuatRiwayat();
            if (UI.Tanya("Pelunasan berhasil. Cetak nota?")) ReportService.TampilkanNota(idP);
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

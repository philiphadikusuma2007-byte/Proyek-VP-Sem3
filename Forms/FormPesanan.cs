using System.Data;

namespace SuryaMandiriPrint;

/// <summary>
/// ADMIN DEPAN - Form Pesanan.
/// MASTER  = daftar pesanan (nota): input, monitoring status, riwayat.
/// DETAIL  = rincian cetak per pesanan (detail_cetak): bahan, sisi, halaman, copy, file.
/// Harga dihitung otomatis oleh TRIGGER MySQL berdasarkan daftar harga; penyimpanan memakai TRANSACTION.
/// </summary>
public class FormPesanan : HalamanForm
{
    readonly DataGridView gNota = UI.Grid();
    readonly DataGridView gDetail = UI.Grid();

    readonly TextBox txtPelanggan = new TextBox { Width = 220 };
    readonly TextBox txtCari = new TextBox { Width = 150 };
    readonly TextBox txtFile = new TextBox { Width = 230, ReadOnly = true };
    readonly ComboBox cmbStatus = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox cmbBahan = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox cmbSisi = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown numHalaman = new NumericUpDown { Width = 80, Minimum = 1, Maximum = 100000, Value = 1 };
    readonly NumericUpDown numCopy = new NumericUpDown { Width = 80, Minimum = 1, Maximum = 100000, Value = 1 };
    readonly DateTimePicker dtDari = new DateTimePicker { Width = 105, Format = DateTimePickerFormat.Short };
    readonly DateTimePicker dtSampai = new DateTimePicker { Width = 105, Format = DateTimePickerFormat.Short };
    readonly GroupBox boxDetail = new GroupBox { Text = "Detail", Dock = DockStyle.Fill };
    readonly Label lblJumlah = new Label { AutoSize = true, Text = "Copy / set" };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 15000 };

    Button btnTambah, btnUbah, btnHapusDetail, btnHapusNota, btnFile;
    DataTable dtBahan;
    bool memuat;
    int idNota;
    string noNota, statusNota;
    int? idDetailEdit;
    long? pilihBaru;
    string fileDipilih;

    public FormPesanan() : base("Form Pesanan")
    {
        Sesi.Wajib(RoleNama.Depan);
        Bangun();
        Load += (s, e) =>
        {
            MuatBahan();
            MuatNota();
            timer.Start();
        };
        timer.Tick += (s, e) => { if (!memuat) MuatNota(); };
    }

    // ================================================================= UI
    void Bangun()
    {
        dtDari.Value = DateTime.Today.AddDays(-30);
        dtSampai.Value = DateTime.Today;
        cmbStatus.Items.AddRange(new object[] { "Semua", "Menunggu", "Diproses", "Selesai", "Tertunda" });
        cmbStatus.SelectedIndex = 0;
        cmbSisi.Items.AddRange(new object[] { "1 Halaman", "Bolak-balik" });
        cmbSisi.SelectedIndex = 0;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(UI.Judul("Form Pesanan (Front Office)"), 0, 0);

        // ---- toolbar master ----
        var bar = UI.Baris();
        bar.Controls.Add(UI.Field("Nama pelanggan", txtPelanggan));
        bar.Controls.Add(UI.Tombol("Pesanan Baru", (s, e) => PesananBaru(), UI.Hijau, 130));
        bar.Controls.Add(new Label { Width = 30 });
        bar.Controls.Add(UI.Field("Status", cmbStatus));
        bar.Controls.Add(UI.Field("Cari no. nota / pelanggan", txtCari));
        bar.Controls.Add(UI.Field("Dari", dtDari));
        bar.Controls.Add(UI.Field("Sampai", dtSampai));
        bar.Controls.Add(UI.Tombol("Tampilkan", (s, e) => MuatNota(), UI.Biru, 100));
        root.Controls.Add(bar, 0, 1);

        // ---- master + detail ----
        var tengah = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 62));

        var boxMaster = new GroupBox { Text = "Master: Daftar Pesanan  (monitoring status & riwayat)", Dock = DockStyle.Fill };
        boxMaster.Controls.Add(gNota);
        tengah.Controls.Add(boxMaster, 0, 0);

        // entry detail
        btnFile = UI.Tombol("Pilih File...", (s, e) => PilihFile(), UI.Biru, 100);
        btnTambah = UI.Tombol("Tambah Detail", (s, e) => TambahDetail(), UI.Hijau, 120);
        btnUbah = UI.Tombol("Ubah Detail", (s, e) => UbahDetail(), null, 110);
        btnHapusDetail = UI.Tombol("Hapus Detail", (s, e) => HapusDetail(), UI.Merah, 110);
        btnHapusNota = UI.Tombol("Batalkan Pesanan", (s, e) => HapusNota(), UI.Merah, 140);
        var btnHarga = UI.Tombol("Daftar Harga", (s, e) => LihatHarga(), UI.Gelap, 110);

        var entry = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        entry.Controls.Add(UI.Field("Bahan", cmbBahan));
        entry.Controls.Add(UI.Field("Sisi cetak", cmbSisi));
        entry.Controls.Add(UI.Field("Jumlah halaman", numHalaman));
        var fCopy = new Panel { Width = 96, Height = 54, Margin = new Padding(4) };
        lblJumlah.Left = 0; lblJumlah.Top = 0;
        numCopy.Left = 0; numCopy.Top = 22;
        fCopy.Controls.Add(lblJumlah);
        fCopy.Controls.Add(numCopy);
        entry.Controls.Add(fCopy);
        entry.Controls.Add(UI.Field("File pesanan (diupload ke server)", txtFile));
        entry.Controls.Add(btnFile);
        entry.Controls.Add(btnTambah);
        entry.Controls.Add(btnUbah);
        entry.Controls.Add(btnHapusDetail);
        entry.Controls.Add(btnHapusNota);
        entry.Controls.Add(btnHarga);

        boxDetail.Controls.Add(gDetail);   // Fill dulu
        boxDetail.Controls.Add(entry);     // lalu Top
        tengah.Controls.Add(boxDetail, 0, 1);
        root.Controls.Add(tengah, 0, 2);
        Controls.Add(root);

        // ---- event ----
        UI.WarnaStatus(gNota, "status_pesanan");
        gNota.SelectionChanged += (s, e) => MuatDetail();
        gDetail.CellClick += (s, e) => { if (e.RowIndex >= 0) IsiFormDariDetail(); };
        gDetail.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) UI.BukaFile(UI.TeksTerpilih(gDetail, "path_file")); };
        cmbBahan.SelectedIndexChanged += (s, e) => AturBahan();
        AturTombol();
    }

    // ================================================================= DATA
    void MuatBahan()
    {
        dtBahan = Db.Tabel(
            "SELECT k.id_kertas, k.kategori, k.satuan, " +
            "CONCAT(k.nama_kertas, CASE k.kategori WHEN 'LEMBAR' THEN '' ELSE CONCAT(' [', k.satuan, ']') END) AS label, " +
            "EXISTS(SELECT 1 FROM harga_kertas h WHERE h.id_kertas = k.id_kertas AND h.sisi = 2) AS ada_sisi2 " +
            "FROM kertas k WHERE k.aktif = 1 ORDER BY k.kategori, k.id_kertas");
        cmbBahan.DataSource = dtBahan;
        cmbBahan.DisplayMember = "label";
        cmbBahan.ValueMember = "id_kertas";
        AturBahan();
    }

    void AturBahan()
    {
        if (cmbBahan.SelectedItem is not DataRowView v) return;
        bool lembar = Convert.ToString(v["kategori"]) == "LEMBAR";
        bool ada2 = UI.Int(v["ada_sisi2"]) == 1;
        cmbSisi.Enabled = lembar && ada2;
        if (!(lembar && ada2)) cmbSisi.SelectedIndex = 0;
        numHalaman.Enabled = lembar;
        if (!lembar) numHalaman.Value = 1;
        lblJumlah.Text = lembar ? "Copy" : "Jumlah (" + v["satuan"] + ")";
    }

    void MuatNota()
    {
        memuat = true;
        try
        {
            int? pilih = pilihBaru.HasValue ? (int)pilihBaru.Value : UI.IdTerpilih(gNota, "id_nota");
            pilihBaru = null;
            string status = cmbStatus.SelectedIndex > 0 ? cmbStatus.Text : null;
            string q = txtCari.Text.Trim();

            var dt = Db.Tabel(
                "SELECT id_nota, no_nota, tanggal, nama_pelanggan, jumlah_file, total_print, total_harga, status_pesanan, catatan " +
                "FROM nota WHERE tanggal >= @a AND tanggal < @b " +
                "AND (@s IS NULL OR status_pesanan = @s) " +
                "AND (@q = '' OR no_nota LIKE @lk OR nama_pelanggan LIKE @lk) ORDER BY id_nota DESC",
                ("@a", dtDari.Value.Date), ("@b", dtSampai.Value.Date.AddDays(1)),
                ("@s", status), ("@q", q), ("@lk", "%" + q + "%"));

            gNota.DataSource = dt;
            UI.Sembunyi(gNota, "id_nota");
            UI.Kol(gNota, "no_nota", "No. Nota", 90);
            UI.Kol(gNota, "tanggal", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(gNota, "nama_pelanggan", "Pelanggan", 130);
            UI.Kol(gNota, "jumlah_file", "File", 40, false, "N0", true);
            UI.Kol(gNota, "total_print", "Total Lembar", 70, false, "N0", true);
            UI.Kol(gNota, "total_harga", "Total Harga (Rp)", 90, true);
            UI.Kol(gNota, "status_pesanan", "Status", 80);
            UI.Kol(gNota, "catatan", "Catatan", 140);

            if (pilih.HasValue)
                foreach (DataGridViewRow r in gNota.Rows)
                    if (UI.Int(r.Cells["id_nota"].Value) == pilih.Value)
                    {
                        gNota.CurrentCell = r.Cells["no_nota"];
                        r.Selected = true;
                        break;
                    }
        }
        catch (Exception ex) { timer.Stop(); UI.Error(ex); }
        finally { memuat = false; }
        MuatDetail();
    }

    void MuatDetail()
    {
        if (memuat) return;
        var id = UI.IdTerpilih(gNota, "id_nota");
        if (id == null)
        {
            idNota = 0; noNota = null; statusNota = null;
            gDetail.DataSource = null;
            boxDetail.Text = "Detail: (pilih pesanan)";
            AturTombol();
            return;
        }

        idNota = id.Value;
        idDetailEdit = null;
        noNota = UI.TeksTerpilih(gNota, "no_nota");
        statusNota = UI.TeksTerpilih(gNota, "status_pesanan");
        boxDetail.Text = "Detail: " + noNota + " - " + UI.TeksTerpilih(gNota, "nama_pelanggan") + "  [" + statusNota + "]";

        try
        {
            gDetail.DataSource = Db.Tabel(
                "SELECT d.id_detail, d.id_kertas, d.path_file, d.sisi, d.nama_file, k.nama_kertas, " +
                "CASE WHEN k.kategori <> 'LEMBAR' THEN '-' WHEN d.sisi = 1 THEN '1 Halaman' ELSE 'Bolak-balik' END AS jenis_sisi, " +
                "d.jumlah_halaman, d.jumlah_copy, d.total_lembar, d.harga_satuan, d.subtotal " +
                "FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas WHERE d.id_nota = @n ORDER BY d.id_detail",
                ("@n", idNota));
            UI.Sembunyi(gDetail, "id_detail", "id_kertas", "path_file", "sisi");
            UI.Kol(gDetail, "nama_file", "File", 140);
            UI.Kol(gDetail, "nama_kertas", "Bahan", 160);
            UI.Kol(gDetail, "jenis_sisi", "Sisi", 70);
            UI.Kol(gDetail, "jumlah_halaman", "Halaman", 55, false, "N0", true);
            UI.Kol(gDetail, "jumlah_copy", "Copy", 45, false, "N0", true);
            UI.Kol(gDetail, "total_lembar", "Total Lembar", 60, false, "N0", true);
            UI.Kol(gDetail, "harga_satuan", "Harga Satuan (Rp)", 80, true);
            UI.Kol(gDetail, "subtotal", "Subtotal (Rp)", 90, true);
        }
        catch (Exception ex) { UI.Error(ex); }
        AturTombol();
    }

    void AturTombol()
    {
        bool bisaUbah = idNota > 0 && statusNota == "Menunggu";
        btnTambah.Enabled = bisaUbah;
        btnFile.Enabled = bisaUbah;
        btnHapusNota.Enabled = bisaUbah;
        btnHapusDetail.Enabled = bisaUbah && gDetail.CurrentRow != null;
        btnUbah.Enabled = bisaUbah && idDetailEdit != null;
        cmbBahan.Enabled = numCopy.Enabled = bisaUbah;
        if (!bisaUbah) { cmbSisi.Enabled = false; numHalaman.Enabled = false; }
        else AturBahan();
    }

    void IsiFormDariDetail()
    {
        if (gDetail.CurrentRow == null || statusNota != "Menunggu") { idDetailEdit = null; AturTombol(); return; }
        idDetailEdit = UI.IdTerpilih(gDetail, "id_detail");
        var idK = UI.IdTerpilih(gDetail, "id_kertas");
        if (idK != null) cmbBahan.SelectedValue = idK.Value;
        int sisi = UI.IdTerpilih(gDetail, "sisi") ?? 1;
        cmbSisi.SelectedIndex = Math.Clamp(sisi - 1, 0, 1);
        numHalaman.Value = Math.Max(1, UI.IdTerpilih(gDetail, "jumlah_halaman") ?? 1);
        numCopy.Value = Math.Max(1, UI.IdTerpilih(gDetail, "jumlah_copy") ?? 1);
        fileDipilih = null;
        txtFile.Text = "";
        AturTombol();
    }

    // ================================================================= AKSI
    void PesananBaru()
    {
        try
        {
            Sesi.Wajib(RoleNama.Depan);
            string nama = txtPelanggan.Text.Trim();
            if (nama == "") { UI.Info("Isi nama pelanggan terlebih dahulu."); return; }

            // no_nota dikosongkan -> dibuat otomatis oleh trigger trg_nota_bi
            pilihBaru = Db.Sisip("INSERT INTO nota (no_nota, tanggal, nama_pelanggan, id_user) VALUES ('', NOW(), @n, @u)",
                                 ("@n", nama), ("@u", Sesi.IdUser));
            txtPelanggan.Clear();
            cmbStatus.SelectedIndex = 0;
            dtSampai.Value = DateTime.Today;
            MuatNota();
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void PilihFile()
    {
        using var d = new OpenFileDialog
        {
            Title = "Pilih file pesanan",
            Filter = "File cetak|*.pdf;*.jpg;*.jpeg;*.png;*.psd;*.cdr;*.ai;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx|Semua file|*.*"
        };
        if (d.ShowDialog() != DialogResult.OK) return;
        fileDipilih = d.FileName;
        txtFile.Text = d.FileName;
    }

    static string SalinKeServer(string sumber, string nota)
    {
        string folder = Path.Combine(AppConfig.Current.FolderFile, nota);
        Directory.CreateDirectory(folder);
        string tujuan = Path.Combine(folder, Path.GetFileName(sumber));
        if (File.Exists(tujuan))
            tujuan = Path.Combine(folder, DateTime.Now.ToString("HHmmss") + "_" + Path.GetFileName(sumber));
        File.Copy(sumber, tujuan, false);
        return tujuan;
    }

    (int idKertas, int sisi) AmbilBahan()
    {
        int idK = Convert.ToInt32(cmbBahan.SelectedValue);
        return (idK, cmbSisi.Enabled ? cmbSisi.SelectedIndex + 1 : 1);
    }

    void TambahDetail()
    {
        string salinan = null;
        try
        {
            Sesi.Wajib(RoleNama.Depan);
            if (idNota == 0 || statusNota != "Menunggu") { UI.Info("Pilih pesanan berstatus Menunggu."); return; }
            var (idK, sisi) = AmbilBahan();

            string namaFile = "(tanpa file)";
            if (!string.IsNullOrEmpty(fileDipilih))
            {
                salinan = SalinKeServer(fileDipilih, noNota);
                namaFile = Path.GetFileName(salinan);
            }

            int idPesanan = idNota;
            Db.Transaksi((c, t) =>
            {
                Db.Jalankan(c, t,
                    "INSERT INTO detail_cetak (id_nota, id_kertas, nama_file, path_file, jumlah_halaman, jumlah_copy, sisi) " +
                    "VALUES (@n, @k, @f, @p, @h, @c, @s)",
                    ("@n", idPesanan), ("@k", idK), ("@f", namaFile), ("@p", salinan),
                    ("@h", (int)numHalaman.Value), ("@c", (int)numCopy.Value), ("@s", sisi));
                Db.Jalankan(c, t, "CALL sp_hitung_ulang_nota(@n)", ("@n", idPesanan));
            });

            fileDipilih = null; txtFile.Text = "";
            pilihBaru = idNota;
            MuatNota();
        }
        catch (Exception ex)
        {
            if (salinan != null) { try { File.Delete(salinan); } catch { } }   // batalkan file bila database gagal
            UI.Error(ex);
        }
    }

    void UbahDetail()
    {
        string salinan = null;
        try
        {
            Sesi.Wajib(RoleNama.Depan);
            if (idDetailEdit == null || statusNota != "Menunggu") return;
            var (idK, sisi) = AmbilBahan();
            int idD = idDetailEdit.Value, idPesanan = idNota;

            string namaFile = null;
            if (!string.IsNullOrEmpty(fileDipilih))
            {
                salinan = SalinKeServer(fileDipilih, noNota);
                namaFile = Path.GetFileName(salinan);
            }

            Db.Transaksi((c, t) =>
            {
                Db.Jalankan(c, t,
                    "UPDATE detail_cetak SET id_kertas=@k, sisi=@s, jumlah_halaman=@h, jumlah_copy=@c, " +
                    "nama_file=COALESCE(@f, nama_file), path_file=COALESCE(@p, path_file) WHERE id_detail=@d",
                    ("@k", idK), ("@s", sisi), ("@h", (int)numHalaman.Value), ("@c", (int)numCopy.Value),
                    ("@f", namaFile), ("@p", salinan), ("@d", idD));
                Db.Jalankan(c, t, "CALL sp_hitung_ulang_nota(@n)", ("@n", idPesanan));
            });

            fileDipilih = null; txtFile.Text = "";
            pilihBaru = idNota;
            MuatNota();
        }
        catch (Exception ex)
        {
            if (salinan != null) { try { File.Delete(salinan); } catch { } }
            UI.Error(ex);
        }
    }

    void HapusDetail()
    {
        try
        {
            Sesi.Wajib(RoleNama.Depan);
            var idD = UI.IdTerpilih(gDetail, "id_detail");
            if (idD == null || statusNota != "Menunggu") return;
            if (!UI.Tanya("Hapus detail cetak yang dipilih?")) return;
            int idPesanan = idNota;

            Db.Transaksi((c, t) =>
            {
                Db.Jalankan(c, t, "DELETE FROM detail_cetak WHERE id_detail=@d", ("@d", idD.Value));
                Db.Jalankan(c, t, "CALL sp_hitung_ulang_nota(@n)", ("@n", idPesanan));   // harga global bisa berubah
            });
            pilihBaru = idNota;
            idDetailEdit = null;
            MuatNota();
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void HapusNota()
    {
        try
        {
            Sesi.Wajib(RoleNama.Depan);
            if (idNota == 0 || statusNota != "Menunggu") return;
            if (!UI.Tanya("Batalkan dan hapus pesanan " + noNota + " beserta seluruh detailnya?")) return;
            Db.Jalankan("DELETE FROM nota WHERE id_nota=@n AND status_pesanan='Menunggu'", ("@n", idNota));
            idDetailEdit = null;
            MuatNota();
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void LihatHarga()
    {
        if (cmbBahan.SelectedValue == null) return;
        try
        {
            var dt = Db.Tabel(
                "SELECT CASE h.sisi WHEN 1 THEN '1 Halaman' ELSE 'Bolak-balik' END AS sisi, " +
                "CONCAT(h.qty_min, CASE WHEN h.qty_max >= 999999 THEN ' ke atas' ELSE CONCAT(' - ', h.qty_max) END) AS jumlah, " +
                "h.harga, CASE h.tipe WHEN 'FLAT' THEN 'Flat (total)' ELSE 'Per unit' END AS tipe " +
                "FROM harga_kertas h WHERE h.id_kertas = @k ORDER BY h.sisi, h.qty_min",
                ("@k", Convert.ToInt32(cmbBahan.SelectedValue)));
            UI.TampilTabel("Daftar Harga - " + cmbBahan.Text +
                           "   (di bawah 100 lbr dihitung per file, di atas 100 lbr dihitung global)", dt, g =>
            {
                UI.Kol(g, "sisi", "Sisi"); UI.Kol(g, "jumlah", "Jumlah");
                UI.Kol(g, "harga", "Harga (Rp)", 100, true); UI.Kol(g, "tipe", "Jenis");
            });
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

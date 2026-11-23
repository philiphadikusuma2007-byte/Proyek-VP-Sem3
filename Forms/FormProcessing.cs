using System.Data;

namespace SuryaMandiriPrint;

/// <summary>
/// ADMIN BELAKANG - Form Processing: mengelola proses produksi dan status pesanan
/// (Menunggu -> Diproses -> Selesai / Tertunda). Stok kertas dipotong otomatis oleh trigger MySQL.
/// Tidak menampilkan harga (harga adalah ranah Admin Depan dan Kasir).
/// </summary>
public class FormProcessing : HalamanForm
{
    readonly DataGridView gNota = UI.Grid();
    readonly DataGridView gDetail = UI.Grid();
    readonly ComboBox cmbFilter = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox txtCari = new TextBox { Width = 170 };
    readonly GroupBox boxDetail = new GroupBox { Text = "Detail", Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 15000 };
    bool memuat;
    Button btnProses, btnSelesai, btnTunda;

    public FormProcessing() : base("Form Processing")
    {
        Sesi.Wajib(RoleNama.Belakang);

        cmbFilter.Items.AddRange(new object[] { "Antrian aktif", "Menunggu", "Diproses", "Tertunda", "Selesai", "Semua" });
        cmbFilter.SelectedIndex = 0;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UI.Judul("Form Processing (Back Office)"), 0, 0);

        btnProses = UI.Tombol("Mulai Proses", (s, e) => UbahStatus("Diproses"), UI.Biru, 120);
        btnSelesai = UI.Tombol("Tandai Selesai", (s, e) => UbahStatus("Selesai"), UI.Hijau, 130);
        btnTunda = UI.Tombol("Tunda", (s, e) => UbahStatus("Tertunda"), UI.Merah, 90);

        var bar = UI.Baris();
        bar.Controls.Add(UI.Field("Tampilkan", cmbFilter));
        bar.Controls.Add(UI.Field("Cari no. nota / pelanggan", txtCari));
        bar.Controls.Add(UI.Tombol("Segarkan", (s, e) => MuatNota(), UI.Gelap, 100));
        bar.Controls.Add(new Label { Width = 30 });
        bar.Controls.Add(btnProses);
        bar.Controls.Add(btnSelesai);
        bar.Controls.Add(btnTunda);
        bar.Controls.Add(UI.Tombol("Buka File", (s, e) => UI.BukaFile(UI.TeksTerpilih(gDetail, "path_file")), null, 100));
        root.Controls.Add(bar, 0, 1);

        var tengah = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tengah.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var boxMaster = new GroupBox { Text = "Master: Antrian Pesanan", Dock = DockStyle.Fill };
        boxMaster.Controls.Add(gNota);
        boxDetail.Controls.Add(gDetail);
        tengah.Controls.Add(boxMaster, 0, 0);
        tengah.Controls.Add(boxDetail, 0, 1);
        root.Controls.Add(tengah, 0, 2);
        Controls.Add(root);

        UI.WarnaStatus(gNota, "status_pesanan");
        gNota.SelectionChanged += (s, e) => MuatDetail();
        gDetail.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) UI.BukaFile(UI.TeksTerpilih(gDetail, "path_file")); };
        cmbFilter.SelectedIndexChanged += (s, e) => MuatNota();
        txtCari.TextChanged += (s, e) => MuatNota();

        Load += (s, e) => { MuatNota(); timer.Start(); };
        timer.Tick += (s, e) => { if (!memuat) MuatNota(); };
    }

    void MuatNota()
    {
        memuat = true;
        try
        {
            int? pilih = UI.IdTerpilih(gNota, "id_nota");
            string f = cmbFilter.Text;
            string q = txtCari.Text.Trim();
            var dt = Db.Tabel(
                "SELECT id_nota, no_nota, tanggal, nama_pelanggan, jumlah_file, total_print, status_pesanan, catatan FROM nota " +
                "WHERE (@f = 'Semua' " +
                "   OR (@f = 'Antrian aktif' AND status_pesanan IN ('Menunggu','Diproses','Tertunda')) " +
                "   OR status_pesanan = @f) " +
                "AND (@q = '' OR no_nota LIKE @lk OR nama_pelanggan LIKE @lk) " +
                "ORDER BY FIELD(status_pesanan,'Diproses','Menunggu','Tertunda','Selesai'), id_nota",
                ("@f", f), ("@q", q), ("@lk", "%" + q + "%"));
            gNota.DataSource = dt;
            UI.Sembunyi(gNota, "id_nota");
            UI.Kol(gNota, "no_nota", "No. Nota", 90);
            UI.Kol(gNota, "tanggal", "Tanggal", 100, false, "dd/MM/yyyy HH:mm");
            UI.Kol(gNota, "nama_pelanggan", "Pelanggan", 130);
            UI.Kol(gNota, "jumlah_file", "File", 40, false, "N0", true);
            UI.Kol(gNota, "total_print", "Total Lembar", 70, false, "N0", true);
            UI.Kol(gNota, "status_pesanan", "Status", 80);
            UI.Kol(gNota, "catatan", "Catatan", 150);

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
        string status = UI.TeksTerpilih(gNota, "status_pesanan");
        btnProses.Enabled = id != null && (status == "Menunggu" || status == "Tertunda");
        btnSelesai.Enabled = id != null && status == "Diproses";
        btnTunda.Enabled = id != null && (status == "Menunggu" || status == "Diproses");
        if (id == null) { gDetail.DataSource = null; boxDetail.Text = "Detail: (pilih pesanan)"; return; }

        boxDetail.Text = "Detail: " + UI.TeksTerpilih(gNota, "no_nota") + " - " + UI.TeksTerpilih(gNota, "nama_pelanggan");
        try
        {
            gDetail.DataSource = Db.Tabel(
                "SELECT d.id_detail, d.path_file, d.nama_file, k.nama_kertas, " +
                "CASE WHEN k.kategori <> 'LEMBAR' THEN '-' WHEN d.sisi = 1 THEN '1 Halaman' ELSE 'Bolak-balik' END AS jenis_sisi, " +
                "d.jumlah_halaman, d.jumlah_copy, d.total_lembar, k.stok AS stok_bahan, k.satuan " +
                "FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas WHERE d.id_nota = @n ORDER BY d.id_detail",
                ("@n", id.Value));
            UI.Sembunyi(gDetail, "id_detail", "path_file");
            UI.Kol(gDetail, "nama_file", "File (dobel-klik untuk membuka)", 150);
            UI.Kol(gDetail, "nama_kertas", "Bahan", 160);
            UI.Kol(gDetail, "jenis_sisi", "Sisi", 70);
            UI.Kol(gDetail, "jumlah_halaman", "Halaman", 55, false, "N0", true);
            UI.Kol(gDetail, "jumlah_copy", "Copy", 45, false, "N0", true);
            UI.Kol(gDetail, "total_lembar", "Total Lembar", 60, false, "N0", true);
            UI.Kol(gDetail, "stok_bahan", "Stok Bahan Saat Ini", 70, false, "N0", true);
            UI.Kol(gDetail, "satuan", "Satuan", 45);
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    void UbahStatus(string baru)
    {
        try
        {
            Sesi.Wajib(RoleNama.Belakang);
            var id = UI.IdTerpilih(gNota, "id_nota");
            string lama = UI.TeksTerpilih(gNota, "status_pesanan");
            if (id == null) return;

            bool sah =
                (lama == "Menunggu" && (baru == "Diproses" || baru == "Tertunda")) ||
                (lama == "Diproses" && (baru == "Selesai" || baru == "Tertunda")) ||
                (lama == "Tertunda" && baru == "Diproses");
            if (!sah) { UI.Info("Perubahan status " + lama + " -> " + baru + " tidak diperbolehkan."); return; }

            string catatan = null;
            if (baru == "Tertunda")
            {
                catatan = UI.Input("Alasan penundaan pesanan:", "Tunda Pesanan");
                if (catatan == null) return;
                if (catatan == "") catatan = "Ditunda";
            }
            else if (baru == "Selesai" && !UI.Tanya("Tandai pesanan ini SELESAI? Kasir akan dapat memprosesnya.")) return;

            int idNota = id.Value;
            Db.Transaksi((c, t) =>
            {
                var cur = Db.Tabel(c, t, "SELECT stok_terpotong FROM nota WHERE id_nota=@n FOR UPDATE", ("@n", idNota));
                bool belumDipotong = UI.Int(cur.Rows[0][0]) == 0;

                // trigger trg_nota_bu memotong stok kertas saat pesanan pertama kali Diproses/Selesai
                Db.Jalankan(c, t, "UPDATE nota SET status_pesanan=@s, catatan=COALESCE(@c, catatan) WHERE id_nota=@n",
                            ("@s", baru), ("@c", catatan), ("@n", idNota));

                // tanggal cetak (dasar Print Volume Report) = hari produksi dimulai
                if (belumDipotong && baru != "Tertunda")
                    Db.Jalankan(c, t, "UPDATE detail_cetak SET tanggal_cetak = CURDATE() WHERE id_nota=@n", ("@n", idNota));
            });
            MuatNota();
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

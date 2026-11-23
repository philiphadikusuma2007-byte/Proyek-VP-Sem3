using System.Data;

namespace SuryaMandiriPrint;

/// <summary>
/// ADMIN BELAKANG - Form Stok: menambah stok kertas dan tinta, memantau stok menipis.
/// Stok tinta berkurang otomatis (trigger MySQL) setiap penjualan mencapai kelipatan 10.000 lembar.
/// </summary>
public class FormStok : HalamanForm
{
    readonly DataGridView gKertas = UI.Grid(), gTinta = UI.Grid(), gLogKertas = UI.Grid(), gLogTinta = UI.Grid();
    readonly NumericUpDown numKertas = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 10000000, Value = 100, ThousandsSeparator = true };
    readonly NumericUpDown numTinta = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 100000, Value = 1 };
    readonly Label lblCounter = new Label { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = UI.Gelap, Margin = new Padding(12, 26, 0, 0) };

    public FormStok() : base("Form Stok")
    {
        Sesi.Wajib(RoleNama.Belakang);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UI.Judul("Form Stok (Kertas & Tinta)"), 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuatTab("Stok Kertas", gKertas, gLogKertas, numKertas, "Jumlah tambah", () => TambahStok("Kertas"), null));
        tabs.TabPages.Add(BuatTab("Stok Tinta", gTinta, gLogTinta, numTinta, "Jumlah tambah", () => TambahStok("Tinta"), lblCounter));
        root.Controls.Add(tabs, 0, 1);
        Controls.Add(root);

        UI.WarnaStatus(gKertas, "kondisi");
        UI.WarnaStatus(gTinta, "kondisi");
        Load += (s, e) => Muat();
    }

    TabPage BuatTab(string judul, DataGridView g, DataGridView log, NumericUpDown num, string label, Action tambah, Label info)
    {
        var tp = new TabPage(judul) { Padding = new Padding(8), BackColor = UI.Latar };
        var bar = UI.Baris();
        bar.Dock = DockStyle.Top;
        bar.Controls.Add(UI.Field(label, num));
        bar.Controls.Add(UI.Tombol("Tambah Stok", (s, e) => tambah(), UI.Hijau, 120));
        bar.Controls.Add(UI.Tombol("Segarkan", (s, e) => Muat(), UI.Gelap, 100));
        if (info != null) bar.Controls.Add(info);

        var tabel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        tabel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        tabel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        var b1 = new GroupBox { Text = "Stok saat ini (pilih baris lalu klik Tambah Stok)", Dock = DockStyle.Fill };
        b1.Controls.Add(g);
        var b2 = new GroupBox { Text = "Riwayat perubahan stok (termasuk potongan otomatis dari trigger)", Dock = DockStyle.Fill };
        b2.Controls.Add(log);
        tabel.Controls.Add(b1, 0, 0);
        tabel.Controls.Add(b2, 0, 1);

        tp.Controls.Add(tabel);
        tp.Controls.Add(bar);
        return tp;
    }

    void Muat()
    {
        try
        {
            gKertas.DataSource = Db.Tabel(
                "SELECT id_kertas AS id, kode, nama_kertas AS nama, kategori, satuan, stok, stok_minimum, " +
                "IF(stok <= stok_minimum, 'Menipis', 'Aman') AS kondisi FROM kertas WHERE aktif = 1 ORDER BY kategori, id_kertas");
            UI.Sembunyi(gKertas, "id");
            UI.Kol(gKertas, "kode", "Kode", 70); UI.Kol(gKertas, "nama", "Nama Bahan", 200);
            UI.Kol(gKertas, "kategori", "Kategori", 80); UI.Kol(gKertas, "satuan", "Satuan", 50);
            UI.Kol(gKertas, "stok", "Stok", 60, false, "N0", true);
            UI.Kol(gKertas, "stok_minimum", "Stok Minimum", 70, false, "N0", true);
            UI.Kol(gKertas, "kondisi", "Kondisi", 60);

            gTinta.DataSource = Db.Tabel(
                "SELECT id_tinta AS id, warna AS nama, stok, satuan, stok_minimum, " +
                "IF(stok <= stok_minimum, 'Menipis', 'Aman') AS kondisi FROM tinta ORDER BY id_tinta");
            UI.Sembunyi(gTinta, "id");
            UI.Kol(gTinta, "nama", "Warna Tinta", 150); UI.Kol(gTinta, "stok", "Stok", 60, false, "N0", true);
            UI.Kol(gTinta, "satuan", "Satuan", 60); UI.Kol(gTinta, "stok_minimum", "Stok Minimum", 70, false, "N0", true);
            UI.Kol(gTinta, "kondisi", "Kondisi", 60);

            gLogKertas.DataSource = LogStok("Kertas");
            gLogTinta.DataSource = LogStok("Tinta");
            foreach (var g in new[] { gLogKertas, gLogTinta })
            {
                UI.Kol(g, "waktu", "Waktu", 100, false, "dd/MM/yyyy HH:mm");
                UI.Kol(g, "nama_item", "Item", 120);
                UI.Kol(g, "jumlah", "Jumlah", 50, false, "+#,##0;-#,##0", true);
                UI.Kol(g, "keterangan", "Keterangan", 200);
            }

            var counter = Db.Tabel("SELECT kunci, nilai FROM pengaturan WHERE kunci IN ('counter_lembar_tinta','ambang_lembar_tinta')");
            string c = "0", a = "10000";
            foreach (DataRow r in counter.Rows)
            {
                if (Convert.ToString(r["kunci"]) == "counter_lembar_tinta") c = Convert.ToString(r["nilai"]);
                else a = Convert.ToString(r["nilai"]);
            }
            lblCounter.Text = "Counter penjualan: " + UI.Angka(c) + " / " + UI.Angka(a) +
                              " lembar  (stok tinta -1 setiap kelipatan " + UI.Angka(a) + ")";
        }
        catch (Exception ex) { UI.Error(ex); }
    }

    static DataTable LogStok(string jenis) => Db.Tabel(
        "SELECT l.waktu, l.nama_item, l.jumlah, l.keterangan FROM log_stok l WHERE l.jenis = @j ORDER BY l.id_log DESC LIMIT 100",
        ("@j", jenis));

    void TambahStok(string jenis)
    {
        try
        {
            Sesi.Wajib(RoleNama.Belakang);
            var g = jenis == "Kertas" ? gKertas : gTinta;
            var num = jenis == "Kertas" ? numKertas : numTinta;
            var id = UI.IdTerpilih(g, "id");
            if (id == null) { UI.Info("Pilih item yang akan ditambah stoknya."); return; }
            string nama = UI.TeksTerpilih(g, "nama");
            int jumlah = (int)num.Value;

            Db.Transaksi((c, t) =>
            {
                if (jenis == "Kertas")
                    Db.Jalankan(c, t, "UPDATE kertas SET stok = stok + @j WHERE id_kertas = @id", ("@j", jumlah), ("@id", id.Value));
                else
                    Db.Jalankan(c, t, "UPDATE tinta SET stok = stok + @j WHERE id_tinta = @id", ("@j", jumlah), ("@id", id.Value));

                Db.Jalankan(c, t,
                    "INSERT INTO log_stok (jenis, nama_item, jumlah, keterangan, id_user) VALUES (@jn, @n, @j, 'Penambahan stok manual', @u)",
                    ("@jn", jenis), ("@n", nama), ("@j", jumlah), ("@u", Sesi.IdUser));
            });
            Muat();
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

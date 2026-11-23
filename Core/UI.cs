using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SuryaMandiriPrint;

/// <summary>Kumpulan helper tampilan (tanpa file Designer: semua kontrol dibuat lewat kode).</summary>
public static class UI
{
    public static readonly Color Oranye = Color.FromArgb(232, 137, 12);
    public static readonly Color Gelap = Color.FromArgb(52, 40, 26);
    public static readonly Color Latar = Color.FromArgb(250, 247, 240);
    public static readonly Color Hijau = Color.FromArgb(39, 142, 84);
    public static readonly Color Merah = Color.FromArgb(192, 57, 43);
    public static readonly Color Biru = Color.FromArgb(41, 128, 185);
    public static readonly CultureInfo ID = new CultureInfo("id-ID");

    // ------------------------------------------------------------ format
    public static string Rp(object v)
    {
        decimal d = (v == null || v is DBNull) ? 0m : Convert.ToDecimal(v);
        return (d < 0 ? "-Rp" : "Rp") + Math.Abs(d).ToString("N0", ID);
    }

    public static string Angka(object v)
    {
        decimal d = (v == null || v is DBNull) ? 0m : Convert.ToDecimal(v);
        return d.ToString("N0", ID);
    }

    public static int Int(object v) => (v == null || v is DBNull) ? 0 : Convert.ToInt32(v);

    public static string Sha256(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    // ------------------------------------------------------------ pesan
    public static void Info(string pesan) =>
        MessageBox.Show(pesan, "Surya Mandiri Print", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(Exception ex) =>
        MessageBox.Show(ex.Message, "Tidak dapat diproses", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Tanya(string pesan) =>
        MessageBox.Show(pesan, "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static string Input(string prompt, string judul)
    {
        using var f = new Form
        {
            Text = judul,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 140),
            MaximizeBox = false,
            MinimizeBox = false
        };
        var l = new Label { Text = prompt, Left = 12, Top = 12, Width = 396, Height = 22 };
        var t = new TextBox { Left = 12, Top = 40, Width = 396 };
        var ok = new Button { Text = "OK", Left = 232, Top = 90, Width = 82, DialogResult = DialogResult.OK };
        var batal = new Button { Text = "Batal", Left = 326, Top = 90, Width = 82, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, t, ok, batal });
        f.AcceptButton = ok;
        f.CancelButton = batal;
        return f.ShowDialog() == DialogResult.OK ? t.Text.Trim() : null;
    }

    // ------------------------------------------------------------ kontrol
    public static Button Tombol(string teks, EventHandler klik, Color? warna = null, int lebar = 130)
    {
        var b = new Button
        {
            Text = teks,
            Width = lebar,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = warna ?? Oranye,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 20, 4, 4)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += klik;
        return b;
    }

    /// <summary>Label di atas, kontrol input di bawah.</summary>
    public static Control Field(string label, Control c)
    {
        var p = new Panel { Width = Math.Max(c.Width, 60) + 8, Height = 54, Margin = new Padding(4) };
        var l = new Label { Text = label, AutoSize = true, Left = 0, Top = 0, ForeColor = Gelap };
        c.Left = 0;
        c.Top = 22;
        p.Controls.Add(l);
        p.Controls.Add(c);
        return p;
    }

    public static Label Judul(string teks) => new Label
    {
        Text = teks,
        Dock = DockStyle.Top,
        Height = 44,
        Font = new Font("Segoe UI", 16f, FontStyle.Bold),
        ForeColor = Gelap,
        TextAlign = ContentAlignment.MiddleLeft
    };

    public static FlowLayoutPanel Baris() => new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Margin = new Padding(0)
    };

    public static Panel Kartu(string judul, out Label nilai, Color? aksen = null)
    {
        var p = new Panel { Width = 205, Height = 78, BackColor = Color.White, Margin = new Padding(6) };
        var strip = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = aksen ?? Oranye };
        var lj = new Label
        {
            Text = judul,
            Dock = DockStyle.Top,
            Height = 26,
            Padding = new Padding(10, 6, 0, 0),
            ForeColor = Color.DimGray
        };
        nilai = new Label
        {
            Text = "-",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Gelap,
            Padding = new Padding(10, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        p.Controls.Add(nilai);   // Fill ditambahkan lebih dulu agar didock terakhir
        p.Controls.Add(lj);
        p.Controls.Add(strip);
        return p;
    }

    // ------------------------------------------------------------ DataGridView
    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 32
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Gelap;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 250, 238);
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 213, 128);
        g.DefaultCellStyle.SelectionForeColor = Color.Black;
        return g;
    }

    /// <summary>Atur judul/format kolom. rupiah=true -> format Rp & rata kanan.</summary>
    public static void Kol(DataGridView g, string nama, string judul, int bobot = 100,
                           bool rupiah = false, string format = null, bool kanan = false)
    {
        if (!g.Columns.Contains(nama)) return;
        var c = g.Columns[nama];
        c.HeaderText = judul;
        c.FillWeight = bobot;
        if (rupiah)
        {
            c.DefaultCellStyle.Format = "N0";
            c.DefaultCellStyle.FormatProvider = ID;
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        else if (format != null) c.DefaultCellStyle.Format = format;
        if (kanan) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    public static void Sembunyi(DataGridView g, params string[] nama)
    {
        foreach (var n in nama)
            if (g.Columns.Contains(n)) g.Columns[n].Visible = false;
    }

    public static int? IdTerpilih(DataGridView g, string kolom)
    {
        if (g.CurrentRow == null || !g.Columns.Contains(kolom)) return null;
        var v = g.CurrentRow.Cells[kolom].Value;
        if (v == null || v is DBNull) return null;
        return Convert.ToInt32(v);
    }

    public static string TeksTerpilih(DataGridView g, string kolom)
    {
        if (g.CurrentRow == null || !g.Columns.Contains(kolom)) return null;
        var v = g.CurrentRow.Cells[kolom].Value;
        return (v == null || v is DBNull) ? null : Convert.ToString(v);
    }

    /// <summary>Beri warna baris menurut nilai kolom status (pesanan / pembayaran / stok).</summary>
    public static void WarnaStatus(DataGridView g, string kolom)
    {
        g.DataBindingComplete += (s, e) =>
        {
            if (!g.Columns.Contains(kolom)) return;
            foreach (DataGridViewRow r in g.Rows)
            {
                string st = Convert.ToString(r.Cells[kolom].Value);
                Color? w = st switch
                {
                    "Menunggu" => Color.FromArgb(255, 243, 205),
                    "Diproses" => Color.FromArgb(209, 231, 250),
                    "Selesai" => Color.FromArgb(212, 237, 218),
                    "Tertunda" => Color.FromArgb(248, 215, 218),
                    "Lunas" => Color.FromArgb(212, 237, 218),
                    "Belum Lunas" => Color.FromArgb(248, 215, 218),
                    "Menipis" => Color.FromArgb(248, 215, 218),
                    _ => null
                };
                if (w != null) r.DefaultCellStyle.BackColor = w.Value;
            }
        };
    }

    // ------------------------------------------------------------ file & export
    public static void BukaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { Info("Pesanan ini tidak memiliki file."); return; }
        if (!File.Exists(path)) { Info("File tidak ditemukan di server:\n" + path); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Error(ex); }
    }

    static string Esc(string s)
    {
        s ??= "";
        return (s.Contains(';') || s.Contains('"') || s.Contains('\n')) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Export tabel ke CSV (pemisah titik-koma, bisa langsung dibuka di Excel).</summary>
    public static void SimpanCsv(DataTable dt, string namaDefault)
    {
        if (dt == null || dt.Rows.Count == 0) { Info("Tidak ada data untuk diexport."); return; }
        using var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = namaDefault + ".csv" };
        if (d.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", dt.Columns.Cast<DataColumn>().Select(c => Esc(c.ColumnName))));
        foreach (DataRow r in dt.Rows)
            sb.AppendLine(string.Join(";", r.ItemArray.Select(x => Esc(Convert.ToString(x, CultureInfo.InvariantCulture)))));
        File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
        Info("Berhasil disimpan:\n" + d.FileName);
    }

    /// <summary>Tampilkan sebuah tabel (read-only) di dialog.</summary>
    public static void TampilTabel(string judul, DataTable dt, Action<DataGridView> format = null)
    {
        using var f = new Form
        {
            Text = judul,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(760, 440),
            Font = new Font("Segoe UI", 9.5f)
        };
        var g = Grid();
        g.DataSource = dt;
        format?.Invoke(g);
        f.Controls.Add(g);
        f.ShowDialog();
    }
}

/// <summary>Kelas dasar semua halaman (Form yang ditanam di panel konten FormMain).</summary>
public abstract class HalamanForm : Form
{
    protected HalamanForm(string judul)
    {
        Text = judul;
        FormBorderStyle = FormBorderStyle.None;
        TopLevel = false;
        Dock = DockStyle.Fill;
        BackColor = UI.Latar;
        Font = new Font("Segoe UI", 9.5f);
    }
}

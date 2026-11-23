namespace SuryaMandiriPrint;

/// <summary>Jendela utama: menu samping dibangun sesuai ROLE pengguna.</summary>
public class FormMain : Form
{
    readonly FlowLayoutPanel menu = new FlowLayoutPanel
    {
        Dock = DockStyle.Left,
        Width = 230,
        BackColor = UI.Gelap,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(0, 10, 0, 0)
    };
    readonly Panel isi = new Panel { Dock = DockStyle.Fill, BackColor = UI.Latar };
    HalamanForm aktif;
    Button tombolAktif;

    public bool Logout { get; private set; }

    public FormMain()
    {
        Text = "Surya Mandiri Digital Print - " + Sesi.Role;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 680);
        Font = new Font("Segoe UI", 9.5f);

        menu.Controls.Add(new Label
        {
            Text = "SURYA MANDIRI",
            ForeColor = Color.FromArgb(255, 196, 0),
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            Width = 230, Height = 40, TextAlign = ContentAlignment.MiddleCenter
        });
        menu.Controls.Add(new Label
        {
            Text = Sesi.Nama + "\n" + Sesi.Role,
            ForeColor = Color.White,
            Width = 230, Height = 46, TextAlign = ContentAlignment.MiddleCenter
        });

        // ---- menu sesuai hak akses (ranah role) ----
        Tambah("Dashboard", () => new FormDashboard());
        if (Sesi.Ada(RoleNama.Depan))
        {
            Tambah("Form Pesanan", () => new FormPesanan());
        }
        if (Sesi.Ada(RoleNama.Belakang))
        {
            Tambah("Form Processing", () => new FormProcessing());
            Tambah("Form Stok", () => new FormStok());
            Tambah("Print Volume Report", () => new FormPrintVolume());
        }
        if (Sesi.Ada(RoleNama.Kasir))
        {
            Tambah("Form Pembayaran", () => new FormPembayaran());
            Tambah("Sales Report", () => new FormSalesReport());
        }

        var keluar = MenuTombol("Logout");
        keluar.BackColor = Color.FromArgb(120, 40, 30);
        keluar.Margin = new Padding(0, 30, 0, 0);
        keluar.Click += (s, e) =>
        {
            if (!UI.Tanya("Keluar dari akun " + Sesi.Username + "?")) return;
            Logout = true;
            Close();
        };
        menu.Controls.Add(keluar);

        Controls.Add(isi);
        Controls.Add(menu);

        Load += (s, e) => { if (menu.Controls.Count > 2) ((Button)menu.Controls[2]).PerformClick(); };
    }

    Button MenuTombol(string teks) => new Button
    {
        Text = "   " + teks,
        Width = 230,
        Height = 46,
        FlatStyle = FlatStyle.Flat,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.White,
        BackColor = UI.Gelap,
        Font = new Font("Segoe UI", 10.5f),
        Margin = new Padding(0),
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderSize = 0 }
    };

    void Tambah(string teks, Func<HalamanForm> buat)
    {
        var b = MenuTombol(teks);
        b.Click += (s, e) =>
        {
            try
            {
                Buka(buat());
                if (tombolAktif != null) tombolAktif.BackColor = UI.Gelap;
                tombolAktif = b;
                b.BackColor = UI.Oranye;
            }
            catch (UnauthorizedAccessException ex) { UI.Error(ex); }
            catch (Exception ex) { UI.Error(ex); }
        };
        menu.Controls.Add(b);
    }

    void Buka(HalamanForm f)
    {
        var lama = aktif;
        isi.Controls.Clear();
        lama?.Dispose();
        aktif = f;
        isi.Controls.Add(f);
        f.Show();
    }
}

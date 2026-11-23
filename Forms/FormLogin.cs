using MySql.Data.MySqlClient;

namespace SuryaMandiriPrint;

public class FormLogin : Form
{
    readonly TextBox txtUser = new TextBox();
    readonly TextBox txtPass = new TextBox { UseSystemPasswordChar = true };

    public FormLogin()
    {
        Text = "Login - Surya Mandiri Digital Print";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(420, 400);
        BackColor = UI.Latar;
        Font = new Font("Segoe UI", 10f);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = UI.Gelap,
            ForeColor = Color.FromArgb(255, 196, 0),
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "SURYA MANDIRI\nDigital Print A3+"
        };

        var lblU = new Label { Text = "Username", Left = 40, Top = 135, AutoSize = true };
        txtUser.SetBounds(40, 160, 340, 28);
        var lblP = new Label { Text = "Password", Left = 40, Top = 203, AutoSize = true };
        txtPass.SetBounds(40, 228, 340, 28);

        var btnMasuk = UI.Tombol("MASUK", (s, e) => Masuk(), null, 340);
        btnMasuk.SetBounds(40, 280, 340, 42);
        btnMasuk.Margin = new Padding(0);

        var btnServer = new Button
        {
            Text = "Pengaturan Server / Koneksi",
            FlatStyle = FlatStyle.Flat,
            ForeColor = UI.Gelap,
            Cursor = Cursors.Hand
        };
        btnServer.FlatAppearance.BorderSize = 0;
        btnServer.SetBounds(40, 335, 340, 30);
        btnServer.Click += (s, e) => { using var f = new FormKoneksi(); f.ShowDialog(this); };

        Controls.AddRange(new Control[] { header, lblU, txtUser, lblP, txtPass, btnMasuk, btnServer });
        AcceptButton = btnMasuk;
    }

    void Masuk()
    {
        try
        {
            var dt = Db.Tabel(
                "SELECT id_user, username, nama_lengkap, `role` AS peran FROM `user` " +
                "WHERE username=@u AND `password`=@p AND aktif=1",
                ("@u", txtUser.Text.Trim()), ("@p", UI.Sha256(txtPass.Text)));

            if (dt.Rows.Count == 0) { UI.Info("Username atau password salah."); return; }

            var r = dt.Rows[0];
            Sesi.IdUser = Convert.ToInt32(r["id_user"]);
            Sesi.Username = Convert.ToString(r["username"]);
            Sesi.Nama = Convert.ToString(r["nama_lengkap"]);
            Sesi.Role = Convert.ToString(r["peran"]);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tidak dapat terhubung ke database MySQL.\nPeriksa 'Pengaturan Server'.\n\n" + ex.Message,
                "Koneksi gagal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>Dialog pengaturan koneksi client ke server MySQL (LAN).</summary>
public class FormKoneksi : Form
{
    readonly TextBox txtServer = new TextBox(), txtPort = new TextBox(), txtDb = new TextBox(),
                     txtUser = new TextBox(), txtPass = new TextBox { UseSystemPasswordChar = true },
                     txtFolder = new TextBox();

    public FormKoneksi()
    {
        Text = "Pengaturan Server";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 330);
        Font = new Font("Segoe UI", 9.5f);

        var a = AppConfig.Current;
        txtServer.Text = a.Server; txtPort.Text = a.Port.ToString(); txtDb.Text = a.Database;
        txtUser.Text = a.User; txtPass.Text = a.Password; txtFolder.Text = a.FolderFile;

        string[] label = { "Server MySQL (IP komputer server)", "Port", "Database", "User database", "Password database", "Folder file pesanan (di server)" };
        TextBox[] kotak = { txtServer, txtPort, txtDb, txtUser, txtPass, txtFolder };
        for (int i = 0; i < kotak.Length; i++)
        {
            Controls.Add(new Label { Text = label[i], Left = 20, Top = 14 + i * 44, AutoSize = true });
            kotak[i].SetBounds(20, 34 + i * 44, 400, 26);
            Controls.Add(kotak[i]);
        }

        var tes = UI.Tombol("Tes Koneksi", (s, e) => Tes(), UI.Biru, 130);
        tes.Location = new Point(20, 285); tes.Margin = new Padding(0);
        var simpan = UI.Tombol("Simpan", (s, e) => Simpan(), null, 130);
        simpan.Location = new Point(290, 285); simpan.Margin = new Padding(0);
        Controls.Add(tes);
        Controls.Add(simpan);
    }

    void Terapkan()
    {
        var a = AppConfig.Current;
        a.Server = txtServer.Text.Trim();
        a.Port = int.TryParse(txtPort.Text, out var p) ? p : 3306;
        a.Database = txtDb.Text.Trim();
        a.User = txtUser.Text.Trim();
        a.Password = txtPass.Text;
        a.FolderFile = txtFolder.Text.Trim();
    }

    void Tes()
    {
        Terapkan();
        try { using var c = Db.Buka(); UI.Info("Koneksi berhasil."); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Koneksi gagal", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void Simpan()
    {
        Terapkan();
        try { AppConfig.Simpan(); UI.Info("Pengaturan disimpan."); Close(); }
        catch (Exception ex) { UI.Error(ex); }
    }
}

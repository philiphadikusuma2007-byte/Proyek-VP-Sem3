# Sistem Manajemen Percetakan SURYA MANDIRI
C# Windows Forms (.NET) + MySQL + FastReport, arsitektur Client-Server (LAN).
Dibuat dari proposal Kelompok 5 dan foto "Daftar Harga Print A3+ Januari 2026".

## 1. Cara menjalankan
1. **Server**: pasang MySQL 8.0+. Jalankan `Database/01_db_percetakan.sql` (Workbench: File > Run SQL Script, atau `mysql -u root -p < 01_db_percetakan.sql`).
   Opsional untuk demo: `Database/02_data_contoh_opsional.sql` (90 pesanan contoh agar laporan & grafik langsung berisi).
2. **Client**: buka `SuryaMandiriPrint.sln` di Visual Studio 2026 (workload ".NET desktop development"). NuGet akan restore otomatis:
   `MySql.Data`, `FastReport.Net.Demo`, `System.Windows.Forms.DataVisualization`.
3. Edit `koneksi.json` (atau tombol **Pengaturan Server** di layar login): isi IP komputer server MySQL dan folder file pesanan
   (boleh path jaringan, mis. `\\SERVER\FilePesanan`). Di server, port 3306 harus terbuka untuk LAN.
4. Jalankan (F5).

| Username | Password | Role |
|---|---|---|
| depan | depan123 | Admin Depan |
| belakang | belakang123 | Admin Belakang |
| kasir | kasir123 | Kasir |

Ganti password dan password akun database `sm_app` (di akhir skrip SQL) sebelum dipakai sungguhan.

## 2. Ranah tiap role
| Fitur | Admin Depan | Admin Belakang | Kasir |
|---|:-:|:-:|:-:|
| Form Pesanan (input, edit, upload file, monitoring, riwayat pesanan) | ✔ | | |
| Form Processing (Diproses / Selesai / Tertunda) | | ✔ | |
| Form Stok (tambah stok kertas & tinta) | | ✔ | |
| Print Volume Report + grafik | | ✔ | |
| Form Pembayaran, nota, riwayat transaksi, status pembayaran, pelunasan tempo | | | ✔ |
| Sales Report + grafik, rekap pendapatan | | | ✔ |

Dijaga di tiga lapis: (1) menu hanya muncul sesuai role, (2) setiap form/aksi memanggil `Sesi.Wajib(...)`,
(3) trigger MySQL menolak perubahan yang melanggar alur (mis. pesanan Selesai tidak bisa diubah, bayar hanya untuk pesanan Selesai).
Admin Belakang sengaja tidak melihat harga; Kasir tidak bisa mengubah pesanan.

## 3. Daftar harga (trigger harga)
Tabel `harga_kertas` berisi seluruh daftar harga dari foto: 7 tingkat jumlah (1-10, 11-30, 31-50, 51-70, 71-90, 91-100, 100 ke atas),
`sisi=1` = harga 1 halaman (angka hitam), `sisi=2` = bolak-balik (angka merah). Ditambah kartu nama dan laminasi.
Harga **dihitung otomatis oleh trigger** `trg_detail_bi` / `trg_detail_bu` (lewat procedure `sp_hitung_baris`) setiap detail pesanan disimpan.
Aturan "di bawah 100 lbr per file, di atas 100 lbr global": jika total lembar satu nota <= 100, tingkat harga ditentukan per file;
jika > 100, semua file memakai tingkat harga dari total lembar nota (kolom "100 ke atas").

## 4. Trigger stok tinta
Setiap ada penjualan baru, jumlah lembar nota ditambahkan ke counter (`pengaturan.counter_lembar_tinta`). Tiap counter mencapai
kelipatan 10.000 lembar, stok **semua warna tinta dikurangi 1** (`trg_penjualan_ai` -> `sp_tambah_counter_tinta`) dan dicatat di `log_stok`.
Ambang bisa diubah di tabel `pengaturan` (`ambang_lembar_tinta`).

## 5. Asumsi yang saya ambil (mohon dicek)
- "mastering" saya artikan **master-detail**: nota (master) dan detail_cetak (detail) pada Form Pesanan, Processing, dan Pembayaran.
- "penjualan mencapai 10.000" = **10.000 lembar** terjual secara kumulatif (bukan Rp10.000). Dihitung saat penjualan dicatat, termasuk tempo.
- Tinta berkurang 1 untuk **setiap warna** (Black, Cyan, Magenta, Yellow). Untuk satu warna saja, ubah `UPDATE tinta` di `sp_tambah_counter_tinta`.
- Kartu nama: 1-10 box harga pertama, **di atas 10 box** harga kedua (foto menulis "10 BOX ke atas", batasnya ambigu). Laminasi: 1-10 = 1.500/sisi, 11-150 = 1.000/sisi, 151-300 = 150.000 flat.
- Stok kertas dipotong sekali, saat pesanan pertama kali **Diproses**. Print Volume dihitung dari pesanan Diproses/Selesai pada tanggal produksi dimulai.
- Kasir hanya bisa membayar pesanan berstatus **Selesai**. Metode: Tunai, Transfer, QRIS, Tempo (dengan jatuh tempo).
- Nama bahan, stok awal, dan harga tinta pada data awal adalah contoh; sesuaikan.

## 6. Catatan teknis
- Semua tampilan dibuat lewat kode (tanpa file Designer), jadi form tidak tampil di Designer tetapi berjalan normal.
- Simpan pesanan, ubah status, bayar, dan tambah stok memakai `Db.Transaksi(...)` (MySQL TRANSACTION: COMMIT / ROLLBACK).
- FastReport: nota 80 mm, Sales Report, dan Print Volume Report dibuat dengan kode di `Reports/ReportService.cs`; grafik dirender dengan Chart control lalu disisipkan sebagai gambar.
  Print dan Export (PDF, Excel, dll.) lewat toolbar jendela preview FastReport. Paket `FastReport.Net.Demo` adalah versi trial (ada watermark);
  jika punya lisensi, ganti dengan referensi DLL FastReport.NET dari installer.
- Target `net10.0-windows`. Jika hanya ada .NET 8, ubah `TargetFramework` di `.csproj` menjadi `net8.0-windows`.

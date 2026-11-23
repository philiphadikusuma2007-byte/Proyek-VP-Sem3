-- =====================================================================
--  db_percetakan  -  Sistem Manajemen Percetakan SURYA MANDIRI
--  Target: MySQL 8.0+  (jalankan sebagai root / user dengan hak penuh)
--  PERINGATAN: skrip ini menghapus lalu membuat ulang database db_percetakan.
-- =====================================================================
DROP DATABASE IF EXISTS db_percetakan;
CREATE DATABASE db_percetakan CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE db_percetakan;

-- ---------------------------------------------------------------------
-- 1. TABEL
-- ---------------------------------------------------------------------
CREATE TABLE `user` (
  id_user       INT AUTO_INCREMENT PRIMARY KEY,
  username      VARCHAR(30)  NOT NULL UNIQUE,
  `password`    CHAR(64)     NOT NULL COMMENT 'SHA-256 hex',
  nama_lengkap  VARCHAR(60)  NOT NULL,
  `role`        ENUM('Admin Depan','Admin Belakang','Kasir') NOT NULL,
  aktif         TINYINT(1)   NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE kertas (
  id_kertas     INT AUTO_INCREMENT PRIMARY KEY,
  kode          VARCHAR(20)  NOT NULL UNIQUE,
  nama_kertas   VARCHAR(80)  NOT NULL,
  kategori      ENUM('LEMBAR','KARTU_NAMA','LAMINASI') NOT NULL DEFAULT 'LEMBAR',
  ukuran        VARCHAR(10)  NOT NULL DEFAULT 'A3+',
  satuan        VARCHAR(10)  NOT NULL DEFAULT 'lembar',
  harga         INT          NOT NULL DEFAULT 0 COMMENT 'harga acuan: tier pertama, 1 halaman',
  stok          INT          NOT NULL DEFAULT 0,
  stok_minimum  INT          NOT NULL DEFAULT 0,
  aktif         TINYINT(1)   NOT NULL DEFAULT 1,
  CONSTRAINT ck_kertas_stok CHECK (stok >= 0)
) ENGINE=InnoDB;

-- Daftar harga bertingkat (sesuai foto "Daftar Harga Print A3+ - Januari 2026")
--   sisi = 1 -> harga 1 halaman  (angka HITAM di foto)
--   sisi = 2 -> harga bolak-balik (angka MERAH di foto)
CREATE TABLE harga_kertas (
  id_harga   INT AUTO_INCREMENT PRIMARY KEY,
  id_kertas  INT        NOT NULL,
  sisi       TINYINT    NOT NULL DEFAULT 1,
  qty_min    INT        NOT NULL,
  qty_max    INT        NOT NULL,
  harga      INT        NOT NULL,
  tipe       ENUM('PER_UNIT','FLAT') NOT NULL DEFAULT 'PER_UNIT',
  UNIQUE KEY uq_tier (id_kertas, sisi, qty_min),
  CONSTRAINT fk_harga_kertas FOREIGN KEY (id_kertas) REFERENCES kertas(id_kertas) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE tinta (
  id_tinta      INT AUTO_INCREMENT PRIMARY KEY,
  warna         VARCHAR(30) NOT NULL UNIQUE,
  stok          INT         NOT NULL DEFAULT 0,
  harga         INT         NOT NULL DEFAULT 0,
  satuan        VARCHAR(20) NOT NULL DEFAULT 'botol',
  stok_minimum  INT         NOT NULL DEFAULT 2,
  CONSTRAINT ck_tinta_stok CHECK (stok >= 0)
) ENGINE=InnoDB;

CREATE TABLE nota (
  id_nota         INT AUTO_INCREMENT PRIMARY KEY,
  no_nota         VARCHAR(20)  NOT NULL DEFAULT '' UNIQUE,
  tanggal         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  nama_pelanggan  VARCHAR(80)  NOT NULL,
  jumlah_file     INT          NOT NULL DEFAULT 0,
  total_print     INT          NOT NULL DEFAULT 0 COMMENT 'total lembar (kategori LEMBAR)',
  total_harga     INT          NOT NULL DEFAULT 0,
  status_pesanan  ENUM('Menunggu','Diproses','Selesai','Tertunda') NOT NULL DEFAULT 'Menunggu',
  stok_terpotong  TINYINT(1)   NOT NULL DEFAULT 0,
  id_user         INT          NULL,
  catatan         VARCHAR(200) NULL,
  KEY idx_nota_tanggal (tanggal),
  KEY idx_nota_status (status_pesanan),
  CONSTRAINT fk_nota_user FOREIGN KEY (id_user) REFERENCES `user`(id_user)
) ENGINE=InnoDB;

CREATE TABLE detail_cetak (
  id_detail       INT AUTO_INCREMENT PRIMARY KEY,
  id_nota         INT          NOT NULL,
  id_kertas       INT          NOT NULL,
  nama_file       VARCHAR(200) NULL,
  path_file       VARCHAR(400) NULL COMMENT 'lokasi file di folder server',
  jumlah_halaman  INT          NOT NULL DEFAULT 1,
  jumlah_copy     INT          NOT NULL DEFAULT 1,
  sisi            TINYINT      NOT NULL DEFAULT 1,
  total_lembar    INT          NOT NULL DEFAULT 0 COMMENT 'diisi trigger: halaman x copy (hanya kategori LEMBAR)',
  harga_satuan    INT          NOT NULL DEFAULT 0 COMMENT 'diisi trigger',
  subtotal        INT          NOT NULL DEFAULT 0 COMMENT 'diisi trigger',
  tanggal_cetak   DATE         NULL,
  KEY idx_detail_tgl (tanggal_cetak),
  CONSTRAINT fk_detail_nota   FOREIGN KEY (id_nota)   REFERENCES nota(id_nota) ON DELETE CASCADE,
  CONSTRAINT fk_detail_kertas FOREIGN KEY (id_kertas) REFERENCES kertas(id_kertas),
  CONSTRAINT ck_detail_qty CHECK (jumlah_halaman > 0 AND jumlah_copy > 0 AND sisi IN (1,2))
) ENGINE=InnoDB;

CREATE TABLE penjualan (
  id_penjualan       INT AUTO_INCREMENT PRIMARY KEY,
  id_nota            INT      NOT NULL UNIQUE,
  tanggal_transaksi  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  total_bayar        INT      NOT NULL DEFAULT 0,
  uang_diterima      INT      NOT NULL DEFAULT 0,
  kembalian          INT      NOT NULL DEFAULT 0,
  metode_pembayaran  ENUM('Tunai','Transfer','QRIS','Tempo') NOT NULL DEFAULT 'Tunai',
  status_pembayaran  ENUM('Lunas','Belum Lunas') NOT NULL DEFAULT 'Lunas',
  jatuh_tempo        DATE     NULL,
  id_user            INT      NULL,
  KEY idx_penjualan_tgl (tanggal_transaksi),
  CONSTRAINT fk_penjualan_nota FOREIGN KEY (id_nota) REFERENCES nota(id_nota),
  CONSTRAINT fk_penjualan_user FOREIGN KEY (id_user) REFERENCES `user`(id_user)
) ENGINE=InnoDB;

CREATE TABLE pengaturan (
  kunci  VARCHAR(40) PRIMARY KEY,
  nilai  VARCHAR(100) NOT NULL
) ENGINE=InnoDB;

CREATE TABLE log_stok (
  id_log      INT AUTO_INCREMENT PRIMARY KEY,
  waktu       DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  jenis       ENUM('Kertas','Tinta') NOT NULL,
  nama_item   VARCHAR(80) NOT NULL,
  jumlah      INT         NOT NULL COMMENT 'positif = tambah, negatif = berkurang',
  keterangan  VARCHAR(150) NULL,
  id_user     INT         NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 2. DATA AWAL
-- ---------------------------------------------------------------------
-- Password (SHA-256):  depan123 / belakang123 / kasir123   -> GANTI setelah instalasi
INSERT INTO `user` (username, `password`, nama_lengkap, `role`) VALUES
 ('depan',    SHA2('depan123',256),    'Admin Depan',    'Admin Depan'),
 ('belakang', SHA2('belakang123',256), 'Admin Belakang', 'Admin Belakang'),
 ('kasir',    SHA2('kasir123',256),    'Kasir',          'Kasir');

INSERT INTO pengaturan (kunci, nilai) VALUES
 ('ambang_lembar_tinta', '10000'),   -- setiap kelipatan 10.000 lembar terjual, stok tinta -1
 ('counter_lembar_tinta', '0');

INSERT INTO tinta (warna, stok, harga, satuan, stok_minimum) VALUES
 ('Black', 10, 110000, 'botol', 2),
 ('Cyan', 10, 110000, 'botol', 2),
 ('Magenta', 10, 110000, 'botol', 2),
 ('Yellow', 10, 110000, 'botol', 2);

-- Bahan per lembar (id 1-15), laminasi (16), kartu nama (17-32). Stok = contoh, silakan ubah.
INSERT INTO kertas (kode, nama_kertas, kategori, satuan, stok, stok_minimum) VALUES
 ('HVS',         'HVS 80/100 / AP 120',                      'LEMBAR', 'lembar', 8000, 1000),
 ('AP150',       'AP 150',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('AP190',       'AP 190',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('AP210',       'AP 210',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('AP230',       'AP 230',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('AP260',       'AP 260',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('AP310',       'AP 310',                                   'LEMBAR', 'lembar', 8000, 1000),
 ('IVORY250',    'Ivory 250 / BC Tik',                       'LEMBAR', 'lembar', 8000, 1000),
 ('IVORY300',    'Ivory 300 / Blushwhite',                   'LEMBAR', 'lembar', 8000, 1000),
 ('LINEN210',    'Linen 210 / Jasmine / Concorde',           'LEMBAR', 'lembar', 8000, 1000),
 ('LINEN230',    'Linen 230 / Glory / Hammer / Copenhagen',  'LEMBAR', 'lembar', 8000, 1000),
 ('BONTAK_MIR',  'Bontak Mir',                               'LEMBAR', 'lembar', 1000, 100),
 ('BONTAK_HVS',  'Bontak HVS',                               'LEMBAR', 'lembar', 1000, 100),
 ('VINIL_SUSU',  'Vinil Susu',                               'LEMBAR', 'lembar', 1000, 100),
 ('VINIL_TRANS', 'Vinil Transparant',                        'LEMBAR', 'lembar', 1000, 100),
 ('LAMINASI',    'Laminasi Doff / Glossy',                   'LAMINASI', 'sisi', 3000, 300),
 ('KN_AP260_1',     'Kartu Nama AP260 1 Sisi',                     'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP260_1_L1',  'Kartu Nama AP260 1 Sisi + Laminasi 1 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP260_1_L2',  'Kartu Nama AP260 1 Sisi + Laminasi 2 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP260_2',     'Kartu Nama AP260 2 Sisi',                     'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP260_2_L1',  'Kartu Nama AP260 2 Sisi + Laminasi 1 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP260_2_L2',  'Kartu Nama AP260 2 Sisi + Laminasi 2 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_1',     'Kartu Nama AP310 1 Sisi',                     'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_1_L1',  'Kartu Nama AP310 1 Sisi + Laminasi 1 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_1_L2',  'Kartu Nama AP310 1 Sisi + Laminasi 2 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_2',     'Kartu Nama AP310 2 Sisi',                     'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_2_L1',  'Kartu Nama AP310 2 Sisi + Laminasi 1 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_AP310_2_L2',  'Kartu Nama AP310 2 Sisi + Laminasi 2 Sisi',   'KARTU_NAMA', 'box', 100, 10),
 ('KN_BW_1',        'Kartu Nama Blushwhite / BC Tik 1 Sisi',       'KARTU_NAMA', 'box', 100, 10),
 ('KN_BW_2',        'Kartu Nama Blushwhite / BC Tik 2 Sisi',       'KARTU_NAMA', 'box', 100, 10),
 ('KN_LJC_1',       'Kartu Nama Linen / Jasmine / Concorde 1 Sisi','KARTU_NAMA', 'box', 100, 10),
 ('KN_LJC_2',       'Kartu Nama Linen / Jasmine / Concorde 2 Sisi','KARTU_NAMA', 'box', 100, 10);

-- Prosedur bantu untuk mengisi tabel harga (dihapus lagi di akhir bagian ini)
DELIMITER $$
CREATE PROCEDURE sp_seed_tier(p_kode VARCHAR(20), p_sisi TINYINT,
                              a INT, b INT, c INT, d INT, e INT, f INT, g INT)
BEGIN
  DECLARE v_id INT;
  SELECT id_kertas INTO v_id FROM kertas WHERE kode = p_kode;
  INSERT INTO harga_kertas (id_kertas, sisi, qty_min, qty_max, harga) VALUES
    (v_id, p_sisi,   1,     10, a),
    (v_id, p_sisi,  11,     30, b),
    (v_id, p_sisi,  31,     50, c),
    (v_id, p_sisi,  51,     70, d),
    (v_id, p_sisi,  71,     90, e),
    (v_id, p_sisi,  91,    100, f),
    (v_id, p_sisi, 101, 999999, g);      -- kolom "100 ke atas"
END$$

CREATE PROCEDURE sp_seed_kartu(p_kode VARCHAR(20), p_1_10 INT, p_10_up INT)
BEGIN
  DECLARE v_id INT;
  SELECT id_kertas INTO v_id FROM kertas WHERE kode = p_kode;
  INSERT INTO harga_kertas (id_kertas, sisi, qty_min, qty_max, harga) VALUES
    (v_id, 1,  1,     10, p_1_10),       -- 1-10 box
    (v_id, 1, 11, 999999, p_10_up);      -- di atas 10 box
END$$
DELIMITER ;

--                      kode          sisi  1-10  11-30 31-50 51-70 71-90 91-100 100+
CALL sp_seed_tier('HVS',        1, 2500, 2400, 2300, 2150, 2000, 1950, 1850);
CALL sp_seed_tier('HVS',        2, 4500, 4300, 4100, 3800, 3600, 3300, 3100);
CALL sp_seed_tier('AP150',      1, 2600, 2500, 2400, 2250, 2150, 2050, 1950);
CALL sp_seed_tier('AP150',      2, 4600, 4400, 4200, 3900, 3700, 3400, 3200);
CALL sp_seed_tier('AP190',      1, 3000, 2800, 2700, 2500, 2400, 2300, 2150);
CALL sp_seed_tier('AP190',      2, 5500, 5200, 4900, 4600, 4300, 4000, 3700);
CALL sp_seed_tier('AP210',      1, 3000, 2850, 2750, 2600, 2500, 2400, 2250);
CALL sp_seed_tier('AP210',      2, 5500, 5300, 5000, 4700, 4400, 4100, 3800);
CALL sp_seed_tier('AP230',      1, 3000, 2900, 2800, 2700, 2600, 2500, 2350);
CALL sp_seed_tier('AP230',      2, 5700, 5400, 5100, 4800, 4500, 4200, 3900);
CALL sp_seed_tier('AP260',      1, 3000, 2950, 2900, 2800, 2700, 2600, 2450);
CALL sp_seed_tier('AP260',      2, 6000, 5700, 5200, 4900, 4600, 4300, 4200);
CALL sp_seed_tier('AP310',      1, 4000, 3900, 3800, 3600, 3400, 3200, 3000);
CALL sp_seed_tier('AP310',      2, 7000, 6800, 6500, 6000, 5500, 5100, 4900);
CALL sp_seed_tier('IVORY250',   1, 4000, 3900, 3800, 3600, 3400, 3200, 3000);
CALL sp_seed_tier('IVORY250',   2, 6500, 6200, 5800, 5500, 5200, 4800, 4500);
CALL sp_seed_tier('IVORY300',   1, 5000, 4700, 4500, 4000, 3700, 3500, 3300);
CALL sp_seed_tier('IVORY300',   2, 7000, 6700, 6300, 5800, 5500, 5000, 4700);
CALL sp_seed_tier('LINEN210',   1, 5000, 4800, 4600, 4300, 4000, 3700, 3500);
CALL sp_seed_tier('LINEN210',   2, 8000, 7800, 7500, 7000, 6500, 6000, 5500);
CALL sp_seed_tier('LINEN230',   1, 5500, 5300, 5000, 4700, 4500, 4200, 4000);
CALL sp_seed_tier('LINEN230',   2, 9000, 8500, 8200, 7200, 6700, 6200, 5700);
CALL sp_seed_tier('BONTAK_MIR', 1, 4500, 4400, 4200, 4000, 3900, 3700, 3500);
CALL sp_seed_tier('BONTAK_HVS', 1, 4000, 3900, 3800, 3700, 3600, 3500, 3400);
CALL sp_seed_tier('VINIL_SUSU', 1, 9000, 8800, 8500, 8000, 7700, 7300, 7000);
CALL sp_seed_tier('VINIL_TRANS',1,10000, 9700, 9200, 8800, 8300, 7800, 7500);

-- Laminasi doff/glossy: 1-10 = 1.500/sisi, 11-150 = 1.000/sisi, 151-300 = 150.000 (flat)
INSERT INTO harga_kertas (id_kertas, sisi, qty_min, qty_max, harga, tipe)
SELECT id_kertas, 1,   1,  10,   1500, 'PER_UNIT' FROM kertas WHERE kode='LAMINASI' UNION ALL
SELECT id_kertas, 1,  11, 150,   1000, 'PER_UNIT' FROM kertas WHERE kode='LAMINASI' UNION ALL
SELECT id_kertas, 1, 151, 300, 150000, 'FLAT'     FROM kertas WHERE kode='LAMINASI';

-- Kartu nama (harga per box):           1-10 box   >10 box
CALL sp_seed_kartu('KN_AP260_1',     19000, 18000);
CALL sp_seed_kartu('KN_AP260_1_L1',  25000, 24000);
CALL sp_seed_kartu('KN_AP260_1_L2',  31000, 30000);
CALL sp_seed_kartu('KN_AP260_2',     29000, 28000);
CALL sp_seed_kartu('KN_AP260_2_L1',  35000, 34000);
CALL sp_seed_kartu('KN_AP260_2_L2',  41000, 40000);
CALL sp_seed_kartu('KN_AP310_1',     22000, 21000);
CALL sp_seed_kartu('KN_AP310_1_L1',  28000, 27000);
CALL sp_seed_kartu('KN_AP310_1_L2',  34000, 33000);
CALL sp_seed_kartu('KN_AP310_2',     34000, 33000);
CALL sp_seed_kartu('KN_AP310_2_L1',  40000, 39000);
CALL sp_seed_kartu('KN_AP310_2_L2',  46000, 45000);
CALL sp_seed_kartu('KN_BW_1',        26000, 25000);
CALL sp_seed_kartu('KN_BW_2',        34000, 33000);
CALL sp_seed_kartu('KN_LJC_1',       27000, 26000);
CALL sp_seed_kartu('KN_LJC_2',       42000, 41000);

DROP PROCEDURE sp_seed_tier;
DROP PROCEDURE sp_seed_kartu;

-- harga acuan di tabel kertas = tier pertama, 1 halaman
UPDATE kertas k SET k.harga = (SELECT h.harga FROM harga_kertas h
                               WHERE h.id_kertas = k.id_kertas AND h.sisi = 1 AND h.qty_min = 1);

-- ---------------------------------------------------------------------
-- 3. STORED PROCEDURE
-- ---------------------------------------------------------------------
DELIMITER $$

-- Menghitung harga satu baris detail_cetak.
--   * Pesanan <= 100 lembar  : tier ditentukan PER FILE (jumlah lembar file itu sendiri)
--   * Pesanan  > 100 lembar  : tier ditentukan GLOBAL (total lembar seluruh file dalam nota)
--     (aturan "DI BAWAH 100 LBR HARGA TERHITUNG PER FILE / DI ATAS 100 LBR HARGA TERHITUNG GLOBAL")
CREATE PROCEDURE sp_hitung_baris(
    IN  p_id_nota INT, IN p_id_detail INT, IN p_id_kertas INT, IN p_sisi TINYINT,
    IN  p_halaman INT, IN p_copy INT,
    OUT o_lembar INT, OUT o_harga INT, OUT o_subtotal INT)
BEGIN
  DECLARE v_kat    VARCHAR(20);
  DECLARE v_unit   INT;
  DECLARE v_lain   INT DEFAULT 0;
  DECLARE v_basis  INT;
  DECLARE v_harga  INT;
  DECLARE v_tipe   VARCHAR(10);

  SELECT kategori INTO v_kat FROM kertas WHERE id_kertas = p_id_kertas;
  IF v_kat IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Bahan tidak ditemukan.';
  END IF;

  SET v_unit = p_halaman * p_copy;
  IF v_unit <= 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Jumlah halaman dan copy harus lebih dari 0.';
  END IF;

  SET v_basis = v_unit;
  IF v_kat = 'LEMBAR' THEN
    SELECT IFNULL(SUM(d.jumlah_halaman * d.jumlah_copy), 0) INTO v_lain
      FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas
     WHERE d.id_nota = p_id_nota AND d.id_detail <> p_id_detail AND k.kategori = 'LEMBAR';
    IF v_lain + v_unit > 100 THEN
      SET v_basis = v_lain + v_unit;           -- harga global (kolom "100 ke atas")
    END IF;
  END IF;

  SELECT harga, tipe INTO v_harga, v_tipe
    FROM harga_kertas
   WHERE id_kertas = p_id_kertas AND sisi = p_sisi
     AND v_basis BETWEEN qty_min AND qty_max
   LIMIT 1;

  IF v_harga IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Harga tidak tersedia untuk bahan / sisi / jumlah tersebut.';
  END IF;

  SET o_lembar = IF(v_kat = 'LEMBAR', v_unit, 0);
  IF v_tipe = 'FLAT' THEN
    SET o_harga    = ROUND(v_harga / v_unit);
    SET o_subtotal = v_harga;
  ELSE
    SET o_harga    = v_harga;
    SET o_subtotal = v_harga * v_unit;
  END IF;
END$$

-- Menjumlah ulang header nota dari detail_cetak
CREATE PROCEDURE sp_total_nota(IN p_id_nota INT)
BEGIN
  UPDATE nota SET
    jumlah_file = (SELECT COUNT(*)                     FROM detail_cetak WHERE id_nota = p_id_nota),
    total_print = (SELECT IFNULL(SUM(total_lembar), 0) FROM detail_cetak WHERE id_nota = p_id_nota),
    total_harga = (SELECT IFNULL(SUM(subtotal), 0)     FROM detail_cetak WHERE id_nota = p_id_nota)
  WHERE id_nota = p_id_nota;
END$$

-- Dipanggil aplikasi (dalam transaksi) setelah detail ditambah/diubah/dihapus,
-- supaya aturan harga global (>100 lembar) diterapkan ke SEMUA file dalam nota.
-- UPDATE "kosong" ini memicu trigger BEFORE UPDATE yang menghitung ulang harga tiap baris.
CREATE PROCEDURE sp_hitung_ulang_nota(IN p_id_nota INT)
BEGIN
  UPDATE detail_cetak SET jumlah_copy = jumlah_copy WHERE id_nota = p_id_nota;
  CALL sp_total_nota(p_id_nota);
END$$

-- Trigger stok tinta: setiap total penjualan kumulatif mencapai kelipatan 10.000 lembar,
-- stok tiap warna tinta dikurangi 1.
CREATE PROCEDURE sp_tambah_counter_tinta(IN p_lembar INT)
BEGIN
  DECLARE v_ambang  INT DEFAULT 10000;
  DECLARE v_counter INT DEFAULT 0;
  DECLARE v_kali    INT DEFAULT 0;

  SELECT CAST(nilai AS UNSIGNED) INTO v_ambang  FROM pengaturan WHERE kunci = 'ambang_lembar_tinta';
  SELECT CAST(nilai AS UNSIGNED) INTO v_counter FROM pengaturan WHERE kunci = 'counter_lembar_tinta' FOR UPDATE;
  IF v_ambang IS NULL OR v_ambang <= 0 THEN SET v_ambang = 10000; END IF;
  IF v_counter IS NULL THEN SET v_counter = 0; END IF;

  SET v_counter = v_counter + p_lembar;
  SET v_kali    = FLOOR(v_counter / v_ambang);
  SET v_counter = v_counter MOD v_ambang;

  UPDATE pengaturan SET nilai = v_counter WHERE kunci = 'counter_lembar_tinta';

  IF v_kali > 0 THEN
    INSERT INTO log_stok (jenis, nama_item, jumlah, keterangan)
      SELECT 'Tinta', warna, -LEAST(stok, v_kali),
             CONCAT('Otomatis: penjualan mencapai ', v_ambang * v_kali, ' lembar')
        FROM tinta;
    UPDATE tinta SET stok = GREATEST(stok - v_kali, 0);
  END IF;
END$$

-- ---------------------------------------------------------------------
-- 4. TRIGGER
-- ---------------------------------------------------------------------

-- ===== NOTA =====
CREATE TRIGGER trg_nota_bi BEFORE INSERT ON nota FOR EACH ROW
BEGIN
  DECLARE v_urut INT;
  IF NEW.tanggal IS NULL THEN SET NEW.tanggal = NOW(); END IF;
  IF NEW.no_nota IS NULL OR NEW.no_nota = '' THEN
    SELECT IFNULL(MAX(CAST(RIGHT(no_nota, 4) AS UNSIGNED)), 0) + 1 INTO v_urut
      FROM nota
     WHERE no_nota LIKE CONCAT('SM-', DATE_FORMAT(NEW.tanggal, '%Y%m%d'), '-%');
    SET NEW.no_nota = CONCAT('SM-', DATE_FORMAT(NEW.tanggal, '%Y%m%d'), '-', LPAD(v_urut, 4, '0'));
  END IF;
  SET NEW.status_pesanan = 'Menunggu';
  SET NEW.stok_terpotong = 0;
  SET NEW.jumlah_file = 0;
  SET NEW.total_print = 0;
  SET NEW.total_harga = 0;
END$$

-- Status pesanan: Selesai bersifat final. Stok kertas dipotong sekali, saat pesanan pertama kali
-- masuk Diproses/Selesai.
CREATE TRIGGER trg_nota_bu BEFORE UPDATE ON nota FOR EACH ROW
BEGIN
  IF OLD.status_pesanan = 'Selesai' AND NEW.status_pesanan <> 'Selesai' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pesanan yang sudah Selesai tidak dapat diubah statusnya.';
  END IF;

  IF NEW.status_pesanan IN ('Diproses', 'Selesai') AND OLD.stok_terpotong = 0 THEN
    IF NOT EXISTS (SELECT 1 FROM detail_cetak WHERE id_nota = OLD.id_nota) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pesanan belum memiliki detail cetak.';
    END IF;

    IF EXISTS (SELECT 1
                 FROM kertas k
                 JOIN (SELECT id_kertas, SUM(jumlah_halaman * jumlah_copy) AS butuh
                         FROM detail_cetak WHERE id_nota = OLD.id_nota GROUP BY id_kertas) x
                   ON x.id_kertas = k.id_kertas
                WHERE k.stok < x.butuh) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Stok kertas tidak mencukupi. Tambah stok kertas terlebih dahulu.';
    END IF;

    INSERT INTO log_stok (jenis, nama_item, jumlah, keterangan)
      SELECT 'Kertas', k.nama_kertas, -SUM(d.jumlah_halaman * d.jumlah_copy), CONCAT('Produksi ', OLD.no_nota)
        FROM detail_cetak d JOIN kertas k ON k.id_kertas = d.id_kertas
       WHERE d.id_nota = OLD.id_nota
       GROUP BY k.id_kertas, k.nama_kertas;

    UPDATE kertas k
      JOIN (SELECT id_kertas, SUM(jumlah_halaman * jumlah_copy) AS butuh
              FROM detail_cetak WHERE id_nota = OLD.id_nota GROUP BY id_kertas) x
        ON x.id_kertas = k.id_kertas
       SET k.stok = k.stok - x.butuh;

    SET NEW.stok_terpotong = 1;
  END IF;
END$$

-- ===== DETAIL CETAK : TRIGGER HARGA =====
CREATE TRIGGER trg_detail_bi BEFORE INSERT ON detail_cetak FOR EACH ROW
BEGIN
  DECLARE v_status VARCHAR(20);
  DECLARE v_tgl    DATE;
  DECLARE v_lembar INT;
  DECLARE v_harga  INT;
  DECLARE v_sub    INT;

  SELECT status_pesanan, DATE(tanggal) INTO v_status, v_tgl FROM nota WHERE id_nota = NEW.id_nota;
  IF v_status IS NULL OR v_status <> 'Menunggu' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Detail cetak hanya dapat ditambah saat status pesanan Menunggu.';
  END IF;

  CALL sp_hitung_baris(NEW.id_nota, 0, NEW.id_kertas, NEW.sisi, NEW.jumlah_halaman, NEW.jumlah_copy,
                       v_lembar, v_harga, v_sub);
  SET NEW.total_lembar = v_lembar;
  SET NEW.harga_satuan = v_harga;
  SET NEW.subtotal     = v_sub;
  IF NEW.tanggal_cetak IS NULL THEN SET NEW.tanggal_cetak = v_tgl; END IF;
END$$

CREATE TRIGGER trg_detail_bu BEFORE UPDATE ON detail_cetak FOR EACH ROW
BEGIN
  DECLARE v_status VARCHAR(20);
  DECLARE v_lembar INT;
  DECLARE v_harga  INT;
  DECLARE v_sub    INT;

  SELECT status_pesanan INTO v_status FROM nota WHERE id_nota = NEW.id_nota;
  IF v_status = 'Menunggu' THEN
    CALL sp_hitung_baris(NEW.id_nota, OLD.id_detail, NEW.id_kertas, NEW.sisi, NEW.jumlah_halaman, NEW.jumlah_copy,
                         v_lembar, v_harga, v_sub);
    SET NEW.total_lembar = v_lembar;
    SET NEW.harga_satuan = v_harga;
    SET NEW.subtotal     = v_sub;
  ELSEIF NEW.id_nota <> OLD.id_nota OR NEW.id_kertas <> OLD.id_kertas OR NEW.sisi <> OLD.sisi
      OR NEW.jumlah_halaman <> OLD.jumlah_halaman OR NEW.jumlah_copy <> OLD.jumlah_copy THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pesanan sudah diproses, detail cetak tidak dapat diubah.';
  END IF;
END$$

CREATE TRIGGER trg_detail_bd BEFORE DELETE ON detail_cetak FOR EACH ROW
BEGIN
  DECLARE v_status VARCHAR(20);
  SELECT status_pesanan INTO v_status FROM nota WHERE id_nota = OLD.id_nota;
  IF v_status <> 'Menunggu' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pesanan sudah diproses, detail cetak tidak dapat dihapus.';
  END IF;
END$$

CREATE TRIGGER trg_detail_ai AFTER INSERT ON detail_cetak FOR EACH ROW
BEGIN
  CALL sp_total_nota(NEW.id_nota);
END$$

CREATE TRIGGER trg_detail_au AFTER UPDATE ON detail_cetak FOR EACH ROW
BEGIN
  CALL sp_total_nota(NEW.id_nota);
END$$

CREATE TRIGGER trg_detail_ad AFTER DELETE ON detail_cetak FOR EACH ROW
BEGIN
  CALL sp_total_nota(OLD.id_nota);
END$$

-- ===== PENJUALAN =====
CREATE TRIGGER trg_penjualan_bi BEFORE INSERT ON penjualan FOR EACH ROW
BEGIN
  DECLARE v_status VARCHAR(20);
  DECLARE v_total  INT;

  SELECT status_pesanan, total_harga INTO v_status, v_total FROM nota WHERE id_nota = NEW.id_nota;
  IF v_status IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nota tidak ditemukan.';
  END IF;
  IF v_status <> 'Selesai' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pembayaran hanya dapat diproses untuk pesanan berstatus Selesai.';
  END IF;
  IF v_total <= 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Total nota 0, tidak dapat dibayar.';
  END IF;

  SET NEW.total_bayar = v_total;
  IF NEW.tanggal_transaksi IS NULL THEN SET NEW.tanggal_transaksi = NOW(); END IF;

  IF NEW.metode_pembayaran = 'Tempo' THEN
    SET NEW.status_pembayaran = 'Belum Lunas';
    SET NEW.uang_diterima = 0;
    SET NEW.kembalian = 0;
    IF NEW.jatuh_tempo IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Jatuh tempo wajib diisi untuk pembayaran tempo.';
    END IF;
  ELSE
    SET NEW.status_pembayaran = 'Lunas';
    SET NEW.jatuh_tempo = NULL;
    IF NEW.metode_pembayaran <> 'Tunai' THEN SET NEW.uang_diterima = v_total; END IF;
    IF NEW.uang_diterima < v_total THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Uang diterima kurang dari total tagihan.';
    END IF;
    SET NEW.kembalian = NEW.uang_diterima - v_total;
  END IF;
END$$

-- Trigger stok tinta: dipicu setiap ada penjualan baru (jumlah lembar nota ditambahkan ke counter).
CREATE TRIGGER trg_penjualan_ai AFTER INSERT ON penjualan FOR EACH ROW
BEGIN
  DECLARE v_lembar INT;
  SELECT total_print INTO v_lembar FROM nota WHERE id_nota = NEW.id_nota;
  IF v_lembar > 0 THEN
    CALL sp_tambah_counter_tinta(v_lembar);
  END IF;
END$$

-- Pelunasan transaksi tempo (Belum Lunas -> Lunas)
CREATE TRIGGER trg_penjualan_bu BEFORE UPDATE ON penjualan FOR EACH ROW
BEGIN
  IF OLD.status_pembayaran = 'Lunas' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Transaksi yang sudah Lunas tidak dapat diubah.';
  END IF;
  SET NEW.id_nota = OLD.id_nota;
  SET NEW.total_bayar = OLD.total_bayar;

  IF NEW.status_pembayaran = 'Lunas' THEN
    IF NEW.metode_pembayaran = 'Tempo' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pilih metode pelunasan: Tunai, Transfer, atau QRIS.';
    END IF;
    IF NEW.metode_pembayaran <> 'Tunai' THEN SET NEW.uang_diterima = OLD.total_bayar; END IF;
    IF NEW.uang_diterima < OLD.total_bayar THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Uang diterima kurang dari total tagihan.';
    END IF;
    SET NEW.kembalian = NEW.uang_diterima - OLD.total_bayar;
    SET NEW.jatuh_tempo = NULL;
  END IF;
END$$

DELIMITER ;

-- ---------------------------------------------------------------------
-- 5. AKUN DATABASE UNTUK APLIKASI (client-server via LAN)
--    Ganti password sebelum dipakai sungguhan, lalu samakan di koneksi.json.
-- ---------------------------------------------------------------------
CREATE USER IF NOT EXISTS 'sm_app'@'%' IDENTIFIED BY 'SuryaMandiri#2026';
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON db_percetakan.* TO 'sm_app'@'%';
FLUSH PRIVILEGES;

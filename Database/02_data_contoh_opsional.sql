-- =====================================================================
--  DATA CONTOH (OPSIONAL) - hanya untuk demo / presentasi.
--  Membuat 90 pesanan selama +/- 45 hari terakhir supaya laporan & grafik berisi data.
--  Data ini ikut memotong stok kertas dan memicu trigger stok tinta,
--  jadi JANGAN dijalankan di database yang sudah berisi data asli.
-- =====================================================================
USE db_percetakan;

DELIMITER $$
CREATE PROCEDURE sp_data_contoh()
BEGIN
  DECLARE i       INT DEFAULT 1;
  DECLARE j       INT;
  DECLARE n_file  INT;
  DECLARE v_id    BIGINT;
  DECLARE v_tgl   DATETIME;
  DECLARE v_total INT;
  DECLARE v_metode VARCHAR(10);

  WHILE i <= 90 DO
    SET v_tgl = TIMESTAMP(DATE_SUB(CURDATE(), INTERVAL (44 - FLOOR((i - 1) / 2)) DAY),
                          MAKETIME(8 + (i MOD 9), (i * 7) MOD 60, 0));

    INSERT INTO nota (no_nota, tanggal, nama_pelanggan, id_user)
    VALUES ('', v_tgl, CONCAT('Pelanggan ', i), 1);
    SET v_id = LAST_INSERT_ID();

    SET n_file = 1 + (i MOD 3);
    SET j = 1;
    WHILE j <= n_file DO
      INSERT INTO detail_cetak (id_nota, id_kertas, nama_file, jumlah_halaman, jumlah_copy, sisi)
      VALUES (v_id, 1 + ((i + j) MOD 11), CONCAT('file_', i, '_', j, '.pdf'),
              5 + ((i * j * 7) MOD 56), 1 + ((i + j) MOD 3), 1 + ((i + j) MOD 2));
      SET j = j + 1;
    END WHILE;

    CALL sp_hitung_ulang_nota(v_id);
    UPDATE nota SET status_pesanan = 'Selesai' WHERE id_nota = v_id;

    SELECT total_harga INTO v_total FROM nota WHERE id_nota = v_id;
    SET v_metode = ELT(1 + (i MOD 3), 'Tunai', 'Transfer', 'QRIS');
    IF i MOD 10 = 0 THEN SET v_metode = 'Tempo'; END IF;

    INSERT INTO penjualan (id_nota, tanggal_transaksi, metode_pembayaran, uang_diterima, jatuh_tempo, id_user)
    VALUES (v_id, DATE_ADD(v_tgl, INTERVAL 2 HOUR), v_metode,
            CEIL(v_total / 1000) * 1000,
            IF(v_metode = 'Tempo', DATE_ADD(CURDATE(), INTERVAL 7 DAY), NULL), 3);

    SET i = i + 1;
  END WHILE;
END$$
DELIMITER ;

CALL sp_data_contoh();
DROP PROCEDURE sp_data_contoh;

SELECT COUNT(*) AS jumlah_nota, SUM(total_print) AS total_lembar, SUM(total_harga) AS total_rupiah FROM nota;
SELECT warna, stok FROM tinta;
SELECT * FROM pengaturan;

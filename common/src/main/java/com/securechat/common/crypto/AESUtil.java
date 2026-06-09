package com.securechat.common.crypto;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import java.nio.ByteBuffer;
import java.security.GeneralSecurityException;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.Base64;

/**
 * AESUtil – Mã hoá/giải mã AES-256-GCM.
 *
 * GCM (Galois/Counter Mode) vừa mã hoá vừa xác thực tính toàn vẹn,
 * không cần padding và chống replay attack bằng IV ngẫu nhiên.
 *
 * Định dạng output: Base64( IV[12 bytes] + Ciphertext + AuthTag[16 bytes] )
 */
public class AESUtil {

    private static final String ALGORITHM    = "AES";
    private static final String CIPHER_MODE  = "AES/GCM/NoPadding";
    private static final int    KEY_SIZE     = 256;   // bits
    private static final int    IV_LENGTH    = 12;    // bytes (96-bit GCM IV)
    private static final int    TAG_LENGTH   = 128;   // bits (16 bytes auth tag)

    // ────────────────────────────────────────────────────────────
    // Sinh khoá AES
    // ────────────────────────────────────────────────────────────

    /**
     * Sinh khoá AES-256 ngẫu nhiên.
     */
    public static SecretKey generateKey() throws NoSuchAlgorithmException {
        KeyGenerator kg = KeyGenerator.getInstance(ALGORITHM);
        kg.init(KEY_SIZE, new SecureRandom());
        return kg.generateKey();
    }

    /**
     * Khôi phục SecretKey từ raw bytes (32 bytes = 256-bit).
     */
    public static SecretKey bytesToKey(byte[] keyBytes) {
        return new SecretKeySpec(keyBytes, ALGORITHM);
    }

    // ────────────────────────────────────────────────────────────
    // Mã hoá
    // ────────────────────────────────────────────────────────────

    /**
     * Mã hoá chuỗi văn bản → chuỗi Base64.
     * IV ngẫu nhiên được nhúng vào đầu output.
     *
     * @param plaintext  nội dung cần mã hoá
     * @param secretKey  khoá AES-256
     * @return           Base64(IV + Ciphertext)
     */
    public static String encrypt(String plaintext, SecretKey secretKey)
            throws GeneralSecurityException {
        byte[] plaintextBytes = plaintext.getBytes(java.nio.charset.StandardCharsets.UTF_8);
        byte[] combined = encryptBytes(plaintextBytes, secretKey);
        return Base64.getEncoder().encodeToString(combined);
    }

    /**
     * Mã hoá byte array (dùng cho file transfer).
     *
     * @param data      dữ liệu cần mã hoá
     * @param secretKey khoá AES-256
     * @return          IV + Ciphertext (raw bytes)
     */
    public static byte[] encryptBytes(byte[] data, SecretKey secretKey)
            throws GeneralSecurityException {
        // Sinh IV ngẫu nhiên
        byte[] iv = new byte[IV_LENGTH];
        new SecureRandom().nextBytes(iv);

        // Mã hoá
        Cipher cipher = Cipher.getInstance(CIPHER_MODE);
        GCMParameterSpec paramSpec = new GCMParameterSpec(TAG_LENGTH, iv);
        cipher.init(Cipher.ENCRYPT_MODE, secretKey, paramSpec);
        byte[] ciphertext = cipher.doFinal(data);

        // Ghép IV + ciphertext
        ByteBuffer buffer = ByteBuffer.allocate(IV_LENGTH + ciphertext.length);
        buffer.put(iv);
        buffer.put(ciphertext);
        return buffer.array();
    }

    // ────────────────────────────────────────────────────────────
    // Giải mã
    // ────────────────────────────────────────────────────────────

    /**
     * Giải mã chuỗi Base64 → plaintext.
     *
     * @param base64Ciphertext  chuỗi Base64(IV + Ciphertext)
     * @param secretKey         khoá AES-256
     * @return                  plaintext
     */
    public static String decrypt(String base64Ciphertext, SecretKey secretKey)
            throws GeneralSecurityException {
        byte[] combined = Base64.getDecoder().decode(base64Ciphertext);
        byte[] plainBytes = decryptBytes(combined, secretKey);
        return new String(plainBytes, java.nio.charset.StandardCharsets.UTF_8);
    }

    /**
     * Giải mã byte array (dùng cho file transfer).
     *
     * @param combined  IV + Ciphertext (raw bytes)
     * @param secretKey khoá AES-256
     * @return          dữ liệu gốc
     */
    public static byte[] decryptBytes(byte[] combined, SecretKey secretKey)
            throws GeneralSecurityException {
        // Tách IV
        ByteBuffer buffer = ByteBuffer.wrap(combined);
        byte[] iv = new byte[IV_LENGTH];
        buffer.get(iv);

        // Phần còn lại là ciphertext
        byte[] ciphertext = new byte[buffer.remaining()];
        buffer.get(ciphertext);

        // Giải mã
        Cipher cipher = Cipher.getInstance(CIPHER_MODE);
        GCMParameterSpec paramSpec = new GCMParameterSpec(TAG_LENGTH, iv);
        cipher.init(Cipher.DECRYPT_MODE, secretKey, paramSpec);
        return cipher.doFinal(ciphertext);
    }

    // ────────────────────────────────────────────────────────────
    // Tiện ích
    // ────────────────────────────────────────────────────────────

    /** Chuyển SecretKey thành Base64 để truyền qua mạng (sau khi mã hoá bằng RSA). */
    public static String keyToBase64(SecretKey key) {
        return Base64.getEncoder().encodeToString(key.getEncoded());
    }

    /** Khôi phục SecretKey từ Base64. */
    public static SecretKey base64ToKey(String base64) {
        byte[] keyBytes = Base64.getDecoder().decode(base64);
        return bytesToKey(keyBytes);
    }
}

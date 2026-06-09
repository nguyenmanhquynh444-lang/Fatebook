package com.securechat.common.crypto;

import javax.crypto.Cipher;
import java.security.*;
import java.security.spec.PKCS8EncodedKeySpec;
import java.security.spec.X509EncodedKeySpec;
import java.util.Base64;

/**
 * RSAUtil – Tiện ích mã hoá/giải mã RSA dùng để trao đổi khoá AES.
 * Thuật toán: RSA-2048 / OAEP / SHA-256
 */
public class RSAUtil {

    private static final String ALGORITHM    = "RSA";
    private static final String CIPHER_MODE  = "RSA/ECB/OAEPWithSHA-256AndMGF1Padding";
    private static final int    KEY_SIZE     = 2048;

    // ────────────────────────────────────────────────────────────
    // Sinh Keypair
    // ────────────────────────────────────────────────────────────

    /**
     * Sinh cặp khoá RSA 2048-bit.
     */
    public static KeyPair generateKeyPair() throws NoSuchAlgorithmException {
        KeyPairGenerator kpg = KeyPairGenerator.getInstance(ALGORITHM);
        kpg.initialize(KEY_SIZE, new SecureRandom());
        return kpg.generateKeyPair();
    }

    // ────────────────────────────────────────────────────────────
    // Mã hoá / Giải mã
    // ────────────────────────────────────────────────────────────

    /**
     * Mã hoá dữ liệu bằng RSA Public Key.
     * @param data      dữ liệu cần mã hoá (thường là AES Key bytes)
     * @param publicKey RSA Public Key
     * @return ciphertext (bytes)
     */
    public static byte[] encrypt(byte[] data, PublicKey publicKey) throws GeneralSecurityException {
        Cipher cipher = Cipher.getInstance(CIPHER_MODE);
        cipher.init(Cipher.ENCRYPT_MODE, publicKey);
        return cipher.doFinal(data);
    }

    /**
     * Giải mã dữ liệu bằng RSA Private Key.
     * @param encryptedData ciphertext
     * @param privateKey    RSA Private Key
     * @return plaintext (bytes)
     */
    public static byte[] decrypt(byte[] encryptedData, PrivateKey privateKey) throws GeneralSecurityException {
        Cipher cipher = Cipher.getInstance(CIPHER_MODE);
        cipher.init(Cipher.DECRYPT_MODE, privateKey);
        return cipher.doFinal(encryptedData);
    }

    // ────────────────────────────────────────────────────────────
    // Serialization (Base64)
    // ────────────────────────────────────────────────────────────

    /** Chuyển Public Key thành chuỗi Base64 để truyền qua mạng. */
    public static String publicKeyToBase64(PublicKey publicKey) {
        return Base64.getEncoder().encodeToString(publicKey.getEncoded());
    }

    /** Chuyển Private Key thành chuỗi Base64 để lưu file. */
    public static String privateKeyToBase64(PrivateKey privateKey) {
        return Base64.getEncoder().encodeToString(privateKey.getEncoded());
    }

    /** Khôi phục Public Key từ chuỗi Base64. */
    public static PublicKey base64ToPublicKey(String base64) throws GeneralSecurityException {
        byte[] keyBytes = Base64.getDecoder().decode(base64);
        KeyFactory kf = KeyFactory.getInstance(ALGORITHM);
        return kf.generatePublic(new X509EncodedKeySpec(keyBytes));
    }

    /** Khôi phục Private Key từ chuỗi Base64. */
    public static PrivateKey base64ToPrivateKey(String base64) throws GeneralSecurityException {
        byte[] keyBytes = Base64.getDecoder().decode(base64);
        KeyFactory kf = KeyFactory.getInstance(ALGORITHM);
        return kf.generatePrivate(new PKCS8EncodedKeySpec(keyBytes));
    }

    // ────────────────────────────────────────────────────────────
    // Ký và xác minh (dùng cho xác thực tin nhắn nếu cần)
    // ────────────────────────────────────────────────────────────

    /** Ký dữ liệu bằng Private Key (SHA256withRSA). */
    public static byte[] sign(byte[] data, PrivateKey privateKey) throws GeneralSecurityException {
        Signature sig = Signature.getInstance("SHA256withRSA");
        sig.initSign(privateKey);
        sig.update(data);
        return sig.sign();
    }

    /** Xác minh chữ ký bằng Public Key. */
    public static boolean verify(byte[] data, byte[] signature, PublicKey publicKey) throws GeneralSecurityException {
        Signature sig = Signature.getInstance("SHA256withRSA");
        sig.initVerify(publicKey);
        sig.update(data);
        return sig.verify(signature);
    }
}

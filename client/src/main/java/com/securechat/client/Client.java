package com.securechat.client;

import com.securechat.client.ui.LoginFrame;
import com.formdev.flatlaf.FlatDarkLaf;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.swing.*;

/**
 * Client – Entry point của Secure Chat Client.
 */
public class Client {

    private static final Logger log = LoggerFactory.getLogger(Client.class);

    public static void main(String[] args) {
        // Cài đặt FlatLaf Dark theme (giao diện hiện đại)
        try {
            FlatDarkLaf.setup();
            UIManager.put("Button.arc", 10);
            UIManager.put("Component.arc", 10);
            UIManager.put("ProgressBar.arc", 10);
            UIManager.put("TextComponent.arc", 8);
        } catch (Exception e) {
            log.warn("Không thể cài FlatLaf: {}", e.getMessage());
        }

        SwingUtilities.invokeLater(() -> {
            log.info("Khởi động Secure Chat Client v1.0");
            new LoginFrame().setVisible(true);
        });
    }
}

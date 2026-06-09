package com.securechat.client.ui;

import com.securechat.client.SecureConnection;
import com.securechat.common.dto.UserDTO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;
import java.awt.event.*;

/**
 * LoginFrame – Giao diện đăng nhập với thiết kế hiện đại.
 *
 * Tính năng:
 * - Kết nối SSL/TLS đến server
 * - Đăng nhập bằng tài khoản được server cấp
 * - Hiển thị trạng thái kết nối (SSL indicator)
 * - Sau khi đăng nhập → mở MainChatFrame
 */
public class LoginFrame extends JFrame {

    private static final Logger log = LoggerFactory.getLogger(LoginFrame.class);

    // ── Màu sắc & style ────────────────────────────────────────
    private static final Color BG_DARK        = new Color(18, 18, 30);
    private static final Color BG_CARD        = new Color(28, 28, 45);
    private static final Color ACCENT         = new Color(99, 102, 241);    // Indigo
    private static final Color ACCENT_HOVER   = new Color(79, 70, 229);
    private static final Color TEXT_PRIMARY   = new Color(248, 250, 252);
    private static final Color TEXT_SECONDARY = new Color(148, 163, 184);
    private static final Color INPUT_BG       = new Color(38, 38, 58);
    private static final Color INPUT_BORDER   = new Color(71, 85, 105);
    private static final Color SUCCESS_GREEN  = new Color(34, 197, 94);
    private static final Color ERROR_RED      = new Color(239, 68, 68);

    // ── Components ──────────────────────────────────────────────
    private JTextField  txtServerHost;
    private JTextField  txtServerPort;
    private JTextField  txtUsername;
    private JPasswordField txtPassword;
    private JButton     btnLogin;
    private JLabel      lblStatus;
    private JLabel      lblSSLIndicator;
    private JProgressBar progressBar;

    // ────────────────────────────────────────────────────────────

    public LoginFrame() {
        setTitle("Secure Chat – Đăng Nhập");
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setSize(460, 580);
        setLocationRelativeTo(null);
        setResizable(false);

        initComponents();
        setupListeners();
    }

    // ────────────────────────────────────────────────────────────
    // Build UI
    // ────────────────────────────────────────────────────────────

    private void initComponents() {
        // Main panel với gradient background
        JPanel mainPanel = new JPanel() {
            @Override
            protected void paintComponent(Graphics g) {
                Graphics2D g2 = (Graphics2D) g;
                g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
                GradientPaint gp = new GradientPaint(
                        0, 0, BG_DARK, getWidth(), getHeight(),
                        new Color(30, 20, 50));
                g2.setPaint(gp);
                g2.fillRect(0, 0, getWidth(), getHeight());
            }
        };
        mainPanel.setLayout(new GridBagLayout());
        mainPanel.setBorder(new EmptyBorder(30, 30, 30, 30));

        // Card panel
        JPanel card = createCard();
        mainPanel.add(card, new GridBagConstraints());

        setContentPane(mainPanel);
    }

    private JPanel createCard() {
        JPanel card = new JPanel();
        card.setBackground(BG_CARD);
        card.setLayout(new BoxLayout(card, BoxLayout.Y_AXIS));
        card.setBorder(new EmptyBorder(35, 40, 35, 40));
        card.setPreferredSize(new Dimension(380, 500));

        // ── Logo & Title ────────────────────────────────────────
        JLabel lblIcon = new JLabel("🔐", SwingConstants.CENTER);
        lblIcon.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 48));
        lblIcon.setAlignmentX(Component.CENTER_ALIGNMENT);

        JLabel lblTitle = new JLabel("Secure Chat");
        lblTitle.setFont(new Font("Segoe UI", Font.BOLD, 26));
        lblTitle.setForeground(TEXT_PRIMARY);
        lblTitle.setAlignmentX(Component.CENTER_ALIGNMENT);

        JLabel lblSubtitle = new JLabel("Mã hoá AES-256 + RSA-2048 + TLS 1.3");
        lblSubtitle.setFont(new Font("Segoe UI", Font.PLAIN, 12));
        lblSubtitle.setForeground(TEXT_SECONDARY);
        lblSubtitle.setAlignmentX(Component.CENTER_ALIGNMENT);

        // ── SSL Indicator ───────────────────────────────────────
        lblSSLIndicator = new JLabel("● Chưa kết nối");
        lblSSLIndicator.setFont(new Font("Segoe UI", Font.PLAIN, 11));
        lblSSLIndicator.setForeground(TEXT_SECONDARY);
        lblSSLIndicator.setAlignmentX(Component.CENTER_ALIGNMENT);

        // ── Separator ───────────────────────────────────────────
        JSeparator sep = new JSeparator();
        sep.setForeground(INPUT_BORDER);
        sep.setMaximumSize(new Dimension(300, 1));

        // ── Server fields ───────────────────────────────────────
        JLabel lblServer = createFieldLabel("Server");

        JPanel serverRow = new JPanel(new BorderLayout(8, 0));
        serverRow.setBackground(BG_CARD);
        serverRow.setMaximumSize(new Dimension(300, 40));

        txtServerHost = createTextField("localhost");
        txtServerPort = createTextField("8443");
        txtServerPort.setPreferredSize(new Dimension(70, 38));

        serverRow.add(txtServerHost, BorderLayout.CENTER);
        serverRow.add(txtServerPort, BorderLayout.EAST);

        // ── Username ────────────────────────────────────────────
        JLabel lblUser = createFieldLabel("Tên đăng nhập");
        txtUsername = createTextField("alice");
        txtUsername.setMaximumSize(new Dimension(300, 38));

        // ── Password ────────────────────────────────────────────
        JLabel lblPwd = createFieldLabel("Mật khẩu");
        txtPassword = new JPasswordField();
        styleTextField(txtPassword);
        txtPassword.setMaximumSize(new Dimension(300, 38));

        // ── Status ──────────────────────────────────────────────
        lblStatus = new JLabel(" ");
        lblStatus.setFont(new Font("Segoe UI", Font.PLAIN, 12));
        lblStatus.setForeground(TEXT_SECONDARY);
        lblStatus.setAlignmentX(Component.CENTER_ALIGNMENT);

        // ── Progress bar ────────────────────────────────────────
        progressBar = new JProgressBar();
        progressBar.setIndeterminate(false);
        progressBar.setVisible(false);
        progressBar.setForeground(ACCENT);
        progressBar.setMaximumSize(new Dimension(300, 4));

        // ── Login button ────────────────────────────────────────
        btnLogin = createLoginButton();

        // ── Assemble ────────────────────────────────────────────
        card.add(lblIcon);
        card.add(Box.createVerticalStrut(8));
        card.add(lblTitle);
        card.add(Box.createVerticalStrut(4));
        card.add(lblSubtitle);
        card.add(Box.createVerticalStrut(6));
        card.add(lblSSLIndicator);
        card.add(Box.createVerticalStrut(20));
        card.add(sep);
        card.add(Box.createVerticalStrut(20));
        card.add(lblServer);
        card.add(Box.createVerticalStrut(5));
        card.add(serverRow);
        card.add(Box.createVerticalStrut(14));
        card.add(lblUser);
        card.add(Box.createVerticalStrut(5));
        card.add(txtUsername);
        card.add(Box.createVerticalStrut(14));
        card.add(lblPwd);
        card.add(Box.createVerticalStrut(5));
        card.add(txtPassword);
        card.add(Box.createVerticalStrut(6));
        card.add(progressBar);
        card.add(Box.createVerticalStrut(6));
        card.add(lblStatus);
        card.add(Box.createVerticalStrut(20));
        card.add(btnLogin);

        return card;
    }

    // ────────────────────────────────────────────────────────────
    // Component factories
    // ────────────────────────────────────────────────────────────

    private JLabel createFieldLabel(String text) {
        JLabel lbl = new JLabel(text);
        lbl.setFont(new Font("Segoe UI", Font.PLAIN, 13));
        lbl.setForeground(TEXT_SECONDARY);
        lbl.setAlignmentX(Component.CENTER_ALIGNMENT);
        lbl.setMaximumSize(new Dimension(300, 20));
        return lbl;
    }

    private JTextField createTextField(String placeholder) {
        JTextField tf = new JTextField(placeholder);
        styleTextField(tf);
        return tf;
    }

    private void styleTextField(JTextField tf) {
        tf.setBackground(INPUT_BG);
        tf.setForeground(TEXT_PRIMARY);
        tf.setCaretColor(TEXT_PRIMARY);
        tf.setFont(new Font("Segoe UI", Font.PLAIN, 14));
        tf.setBorder(BorderFactory.createCompoundBorder(
            BorderFactory.createLineBorder(INPUT_BORDER, 1),
            new EmptyBorder(8, 12, 8, 12)
        ));
        tf.setMaximumSize(new Dimension(300, 38));
        tf.setAlignmentX(Component.CENTER_ALIGNMENT);

        // Focus effect
        tf.addFocusListener(new FocusAdapter() {
            @Override public void focusGained(FocusEvent e) {
                tf.setBorder(BorderFactory.createCompoundBorder(
                    BorderFactory.createLineBorder(ACCENT, 2),
                    new EmptyBorder(7, 11, 7, 11)
                ));
            }
            @Override public void focusLost(FocusEvent e) {
                tf.setBorder(BorderFactory.createCompoundBorder(
                    BorderFactory.createLineBorder(INPUT_BORDER, 1),
                    new EmptyBorder(8, 12, 8, 12)
                ));
            }
        });
    }

    private JButton createLoginButton() {
        JButton btn = new JButton("🔑  Đăng Nhập An Toàn") {
            @Override protected void paintComponent(Graphics g) {
                Graphics2D g2 = (Graphics2D) g;
                g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
                Color bg = getModel().isPressed() ? ACCENT_HOVER :
                           getModel().isRollover() ? ACCENT_HOVER : ACCENT;
                g2.setColor(bg);
                g2.fillRoundRect(0, 0, getWidth(), getHeight(), 10, 10);
                super.paintComponent(g);
            }
        };
        btn.setFont(new Font("Segoe UI", Font.BOLD, 15));
        btn.setForeground(Color.WHITE);
        btn.setContentAreaFilled(false);
        btn.setBorderPainted(false);
        btn.setFocusPainted(false);
        btn.setCursor(new Cursor(Cursor.HAND_CURSOR));
        btn.setMaximumSize(new Dimension(300, 45));
        btn.setAlignmentX(Component.CENTER_ALIGNMENT);
        return btn;
    }

    // ────────────────────────────────────────────────────────────
    // Listeners
    // ────────────────────────────────────────────────────────────

    private void setupListeners() {
        btnLogin.addActionListener(e -> performLogin());

        // Enter key
        KeyAdapter enterKey = new KeyAdapter() {
            @Override public void keyPressed(KeyEvent e) {
                if (e.getKeyCode() == KeyEvent.VK_ENTER) performLogin();
            }
        };
        txtUsername.addKeyListener(enterKey);
        txtPassword.addKeyListener(enterKey);
    }

    private void performLogin() {
        String host     = txtServerHost.getText().trim();
        String portText = txtServerPort.getText().trim();
        String username = txtUsername.getText().trim();
        String password = new String(txtPassword.getPassword());

        if (username.isEmpty() || password.isEmpty()) {
            setStatus("Vui lòng nhập đầy đủ thông tin!", ERROR_RED);
            return;
        }

        int port;
        try {
            port = Integer.parseInt(portText);
        } catch (NumberFormatException ex) {
            setStatus("Port không hợp lệ!", ERROR_RED);
            return;
        }

        setLoading(true, "Đang kết nối SSL/TLS...");

        // Thực hiện login trong background thread
        int finalPort = port;
        SwingWorker<UserDTO, String> worker = new SwingWorker<>() {
            SecureConnection connection;

            @Override
            protected UserDTO doInBackground() throws Exception {
                publish("Thiết lập SSL/TLS...");
                connection = new SecureConnection(host, finalPort);

                publish("Đang xác thực tài khoản...");
                UserDTO user = connection.connect(username, password);

                if (user != null) {
                    publish("Trao đổi khoá RSA/AES...");
                    Thread.sleep(300); // Ngắn để hiển thị trạng thái
                }
                return user;
            }

            @Override
            protected void process(java.util.List<String> chunks) {
                setStatus(chunks.get(chunks.size() - 1), TEXT_SECONDARY);
            }

            @Override
            protected void done() {
                setLoading(false, "");
                try {
                    UserDTO user = get();
                    if (user != null) {
                        // Đăng nhập thành công
                        setSSLIndicator(true);
                        setStatus("✅ Đăng nhập thành công!", SUCCESS_GREEN);

                        // Mở MainChatFrame sau 500ms
                        Timer timer = new Timer(500, evt -> {
                            MainChatFrame chatFrame = new MainChatFrame(connection, user);
                            chatFrame.setVisible(true);
                            LoginFrame.this.dispose();
                        });
                        timer.setRepeats(false);
                        timer.start();
                    } else {
                        setStatus("❌ Sai tên đăng nhập hoặc mật khẩu!", ERROR_RED);
                        txtPassword.setText("");
                    }
                } catch (Exception e) {
                    log.error("Lỗi đăng nhập: {}", e.getMessage(), e);
                    setStatus("❌ Không thể kết nối: " + e.getMessage(), ERROR_RED);
                }
            }
        };
        worker.execute();
    }

    // ────────────────────────────────────────────────────────────
    // UI helpers
    // ────────────────────────────────────────────────────────────

    private void setLoading(boolean loading, String message) {
        btnLogin.setEnabled(!loading);
        progressBar.setVisible(loading);
        progressBar.setIndeterminate(loading);
        if (!message.isEmpty()) setStatus(message, TEXT_SECONDARY);
    }

    private void setStatus(String text, Color color) {
        lblStatus.setText(text);
        lblStatus.setForeground(color);
    }

    private void setSSLIndicator(boolean secure) {
        lblSSLIndicator.setText(secure ? "🔒 TLS 1.3 – Kết nối an toàn" : "● Chưa kết nối");
        lblSSLIndicator.setForeground(secure ? SUCCESS_GREEN : TEXT_SECONDARY);
    }
}

package com.securechat.client.ui;

import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.securechat.client.SecureConnection;
import com.securechat.client.storage.LocalHistoryDB;
import com.securechat.common.dto.MessageDTO;
import com.securechat.common.dto.UserDTO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;
import java.awt.event.*;
import java.io.File;
import java.nio.file.Files;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;

/**
 * MainChatFrame – Giao diện chính sau khi đăng nhập.
 *
 * Layout:
 * ┌──────────────┬────────────────────────────────┐
 * │  Sidebar     │  Chat Area                     │
 * │  - Header    │  - Chat header (user/room)     │
 * │  - User list │  - Message bubbles             │
 * │  - My info   │  - Input bar + Send + File     │
 * └──────────────┴────────────────────────────────┘
 */
public class MainChatFrame extends JFrame {

    private static final Logger log = LoggerFactory.getLogger(MainChatFrame.class);

    // ── Màu sắc ─────────────────────────────────────────────────
    private static final Color BG_MAIN      = new Color(15, 15, 25);
    private static final Color BG_SIDEBAR   = new Color(22, 22, 38);
    private static final Color BG_CHAT      = new Color(18, 18, 30);
    private static final Color BG_INPUT     = new Color(28, 28, 45);
    private static final Color BG_MSG_ME    = new Color(99, 102, 241);   // Tin nhắn của mình
    private static final Color BG_MSG_OTHER = new Color(38, 38, 58);    // Tin nhắn người khác
    private static final Color BG_HOVER     = new Color(35, 35, 55);
    private static final Color ACCENT       = new Color(99, 102, 241);
    private static final Color TEXT_PRIMARY = new Color(248, 250, 252);
    private static final Color TEXT_SECOND  = new Color(148, 163, 184);
    private static final Color DIVIDER      = new Color(40, 40, 65);
    private static final Color ONLINE_DOT   = new Color(34, 197, 94);

    // ── State ───────────────────────────────────────────────────
    private final SecureConnection connection;
    private final UserDTO          me;
    private final LocalHistoryDB   historyDB;
    private final Gson             gson = new Gson();

    private UserDTO     selectedUser;    // User đang chat với
    private int         currentRoomId;   // Room ID hiện tại

    // ── Components ──────────────────────────────────────────────
    private DefaultListModel<UserDTO> userListModel;
    private JList<UserDTO>            userList;
    private JPanel                    chatPanel;
    private JScrollPane               chatScrollPane;
    private JTextField                txtInput;
    private JLabel                    lblChatWith;
    private JLabel                    lblEncStatus;
    private JButton                   btnSend;
    private JButton                   btnFile;
    private JButton                   btnHistory;

    // ────────────────────────────────────────────────────────────

    public MainChatFrame(SecureConnection connection, UserDTO me) {
        this.connection = connection;
        this.me         = me;
        this.historyDB  = new LocalHistoryDB(me.getUsername());
        historyDB.init();

        setTitle("Secure Chat – " + me.getDisplayName());
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setSize(1100, 720);
        setMinimumSize(new Dimension(800, 500));
        setLocationRelativeTo(null);

        initComponents();
        setupMessageHandler();
        setupListeners();

        // Window close → disconnect
        addWindowListener(new WindowAdapter() {
            @Override public void windowClosing(WindowEvent e) {
                connection.disconnect();
                historyDB.close();
            }
        });
    }

    // ────────────────────────────────────────────────────────────
    // Build UI
    // ────────────────────────────────────────────────────────────

    private void initComponents() {
        JPanel root = new JPanel(new BorderLayout(0, 0));
        root.setBackground(BG_MAIN);

        root.add(buildSidebar(), BorderLayout.WEST);
        root.add(buildChatArea(), BorderLayout.CENTER);

        setContentPane(root);
    }

    // ── Sidebar ─────────────────────────────────────────────────

    private JPanel buildSidebar() {
        JPanel sidebar = new JPanel(new BorderLayout(0, 0));
        sidebar.setBackground(BG_SIDEBAR);
        sidebar.setPreferredSize(new Dimension(260, 0));
        sidebar.setBorder(BorderFactory.createMatteBorder(0, 0, 0, 1, DIVIDER));

        // Header
        JPanel header = new JPanel(new BorderLayout());
        header.setBackground(BG_SIDEBAR);
        header.setBorder(new EmptyBorder(18, 16, 14, 16));

        JLabel lblApp = new JLabel("🔐 Secure Chat");
        lblApp.setFont(new Font("Segoe UI", Font.BOLD, 16));
        lblApp.setForeground(TEXT_PRIMARY);

        JLabel lblVer = new JLabel("v1.0 – AES/RSA");
        lblVer.setFont(new Font("Segoe UI", Font.PLAIN, 11));
        lblVer.setForeground(TEXT_SECOND);

        header.add(lblApp, BorderLayout.NORTH);
        header.add(lblVer, BorderLayout.SOUTH);

        // Separator
        JSeparator sep1 = new JSeparator();
        sep1.setForeground(DIVIDER);

        // Section label
        JLabel lblContacts = new JLabel("  NGƯỜI DÙNG ONLINE");
        lblContacts.setFont(new Font("Segoe UI", Font.BOLD, 10));
        lblContacts.setForeground(TEXT_SECOND);
        lblContacts.setBorder(new EmptyBorder(10, 0, 6, 0));

        // User list
        userListModel = new DefaultListModel<>();
        userList = new JList<>(userListModel);
        userList.setCellRenderer(new UserListCellRenderer());
        userList.setBackground(BG_SIDEBAR);
        userList.setForeground(TEXT_PRIMARY);
        userList.setSelectionBackground(BG_HOVER);
        userList.setFixedCellHeight(58);
        userList.setBorder(null);
        userList.setFocusable(false);

        JScrollPane listScroll = new JScrollPane(userList);
        listScroll.setBorder(null);
        listScroll.getViewport().setBackground(BG_SIDEBAR);
        listScroll.setVerticalScrollBarPolicy(ScrollPaneConstants.VERTICAL_SCROLLBAR_AS_NEEDED);

        // My info panel (bottom)
        JPanel myInfo = buildMyInfoPanel();

        sidebar.add(header,    BorderLayout.NORTH);
        JPanel centerPanel = new JPanel(new BorderLayout());
        centerPanel.setBackground(BG_SIDEBAR);
        centerPanel.add(sep1,         BorderLayout.NORTH);
        centerPanel.add(lblContacts,  BorderLayout.CENTER);
        // wrap
        JPanel listWrapper = new JPanel(new BorderLayout());
        listWrapper.setBackground(BG_SIDEBAR);
        listWrapper.add(lblContacts, BorderLayout.NORTH);
        listWrapper.add(listScroll,  BorderLayout.CENTER);

        sidebar.add(listWrapper, BorderLayout.CENTER);
        sidebar.add(myInfo,      BorderLayout.SOUTH);

        return sidebar;
    }

    private JPanel buildMyInfoPanel() {
        JPanel panel = new JPanel(new BorderLayout(10, 0));
        panel.setBackground(new Color(15, 15, 28));
        panel.setBorder(new EmptyBorder(14, 16, 14, 16));

        JLabel avatar = new JLabel("👤");
        avatar.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 28));

        JPanel info = new JPanel(new GridLayout(2, 1, 0, 2));
        info.setBackground(new Color(15, 15, 28));

        JLabel name = new JLabel(me.getDisplayName() != null ? me.getDisplayName() : me.getUsername());
        name.setFont(new Font("Segoe UI", Font.BOLD, 13));
        name.setForeground(TEXT_PRIMARY);

        JLabel status = new JLabel("🟢 Online – TLS 1.3");
        status.setFont(new Font("Segoe UI", Font.PLAIN, 11));
        status.setForeground(ONLINE_DOT);

        info.add(name);
        info.add(status);

        btnHistory = new JButton("📋 Lịch sử");
        btnHistory.setFont(new Font("Segoe UI", Font.PLAIN, 11));
        btnHistory.setForeground(TEXT_SECOND);
        btnHistory.setBackground(BG_INPUT);
        btnHistory.setBorderPainted(false);
        btnHistory.setFocusPainted(false);
        btnHistory.setCursor(new Cursor(Cursor.HAND_CURSOR));

        panel.add(avatar,     BorderLayout.WEST);
        panel.add(info,       BorderLayout.CENTER);
        panel.add(btnHistory, BorderLayout.EAST);

        return panel;
    }

    // ── Chat Area ───────────────────────────────────────────────

    private JPanel buildChatArea() {
        JPanel area = new JPanel(new BorderLayout(0, 0));
        area.setBackground(BG_CHAT);

        // Chat header
        JPanel chatHeader = new JPanel(new BorderLayout(12, 0));
        chatHeader.setBackground(new Color(20, 20, 35));
        chatHeader.setBorder(new EmptyBorder(14, 20, 14, 20));
        chatHeader.setBorder(BorderFactory.createCompoundBorder(
                BorderFactory.createMatteBorder(0, 0, 1, 0, DIVIDER),
                new EmptyBorder(14, 20, 14, 20)));

        JLabel avatarLabel = new JLabel("💬");
        avatarLabel.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 24));

        JPanel headerInfo = new JPanel(new GridLayout(2, 1, 0, 2));
        headerInfo.setBackground(new Color(20, 20, 35));

        lblChatWith = new JLabel("Chọn người để bắt đầu chat");
        lblChatWith.setFont(new Font("Segoe UI", Font.BOLD, 15));
        lblChatWith.setForeground(TEXT_PRIMARY);

        lblEncStatus = new JLabel("🔒 Mã hoá đầu cuối AES-256-GCM");
        lblEncStatus.setFont(new Font("Segoe UI", Font.PLAIN, 11));
        lblEncStatus.setForeground(new Color(34, 197, 94));

        headerInfo.add(lblChatWith);
        headerInfo.add(lblEncStatus);

        chatHeader.add(avatarLabel, BorderLayout.WEST);
        chatHeader.add(headerInfo,  BorderLayout.CENTER);

        // Messages area
        chatPanel = new JPanel();
        chatPanel.setLayout(new BoxLayout(chatPanel, BoxLayout.Y_AXIS));
        chatPanel.setBackground(BG_CHAT);
        chatPanel.setBorder(new EmptyBorder(16, 16, 16, 16));

        chatScrollPane = new JScrollPane(chatPanel);
        chatScrollPane.setBorder(null);
        chatScrollPane.getViewport().setBackground(BG_CHAT);
        chatScrollPane.setVerticalScrollBarPolicy(ScrollPaneConstants.VERTICAL_SCROLLBAR_AS_NEEDED);

        // Input area
        JPanel inputArea = buildInputArea();

        area.add(chatHeader,  BorderLayout.NORTH);
        area.add(chatScrollPane, BorderLayout.CENTER);
        area.add(inputArea,   BorderLayout.SOUTH);

        return area;
    }

    private JPanel buildInputArea() {
        JPanel panel = new JPanel(new BorderLayout(10, 0));
        panel.setBackground(BG_INPUT);
        panel.setBorder(BorderFactory.createCompoundBorder(
                BorderFactory.createMatteBorder(1, 0, 0, 0, DIVIDER),
                new EmptyBorder(12, 16, 12, 16)));

        // File button
        btnFile = createIconButton("📎", "Gửi file");

        // Input field
        txtInput = new JTextField();
        txtInput.setBackground(new Color(38, 38, 58));
        txtInput.setForeground(TEXT_PRIMARY);
        txtInput.setCaretColor(TEXT_PRIMARY);
        txtInput.setFont(new Font("Segoe UI", Font.PLAIN, 14));
        txtInput.setBorder(BorderFactory.createCompoundBorder(
                BorderFactory.createLineBorder(new Color(60, 60, 90), 1),
                new EmptyBorder(9, 14, 9, 14)));
        txtInput.putClientProperty("JTextField.placeholderText",
                "Nhập tin nhắn (được mã hoá AES-256)...");
        txtInput.setEnabled(false);

        // Send button
        btnSend = new JButton("Gửi ➤") {
            @Override protected void paintComponent(Graphics g) {
                Graphics2D g2 = (Graphics2D) g;
                g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
                g2.setColor(isEnabled() ?
                        (getModel().isRollover() ? new Color(79, 70, 229) : ACCENT) :
                        new Color(60, 60, 80));
                g2.fillRoundRect(0, 0, getWidth(), getHeight(), 8, 8);
                super.paintComponent(g);
            }
        };
        btnSend.setFont(new Font("Segoe UI", Font.BOLD, 13));
        btnSend.setForeground(Color.WHITE);
        btnSend.setContentAreaFilled(false);
        btnSend.setBorderPainted(false);
        btnSend.setFocusPainted(false);
        btnSend.setCursor(new Cursor(Cursor.HAND_CURSOR));
        btnSend.setPreferredSize(new Dimension(90, 40));
        btnSend.setEnabled(false);

        panel.add(btnFile,    BorderLayout.WEST);
        panel.add(txtInput,   BorderLayout.CENTER);
        panel.add(btnSend,    BorderLayout.EAST);

        return panel;
    }

    private JButton createIconButton(String icon, String tooltip) {
        JButton btn = new JButton(icon);
        btn.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 20));
        btn.setForeground(TEXT_SECOND);
        btn.setBackground(null);
        btn.setBorderPainted(false);
        btn.setContentAreaFilled(false);
        btn.setFocusPainted(false);
        btn.setCursor(new Cursor(Cursor.HAND_CURSOR));
        btn.setToolTipText(tooltip);
        btn.setPreferredSize(new Dimension(44, 40));
        return btn;
    }

    // ────────────────────────────────────────────────────────────
    // Message handling
    // ────────────────────────────────────────────────────────────

    private void setupMessageHandler() {
        connection.setOnMessageReceived(msg -> {
            SwingUtilities.invokeLater(() -> handleIncomingMessage(msg));
        });

        connection.setOnConnectionError(error -> {
            SwingUtilities.invokeLater(() -> {
                JOptionPane.showMessageDialog(this,
                        "Mất kết nối server: " + error,
                        "Lỗi kết nối", JOptionPane.ERROR_MESSAGE);
            });
        });
    }

    private void handleIncomingMessage(MessageDTO msg) {
        switch (msg.getType()) {
            case USER_LIST -> updateUserList(msg.getPlainContent());
            case TEXT      -> receiveTextMessage(msg);
            case FILE      -> receiveFileMessage(msg);
            case SYSTEM    -> addSystemMessage(msg.getPlainContent());
            default -> log.debug("Nhận message type: {}", msg.getType());
        }
    }

    private void updateUserList(String usersJson) {
        List<UserDTO> users = gson.fromJson(usersJson,
                new TypeToken<List<UserDTO>>(){}.getType());
        userListModel.clear();
        for (UserDTO user : users) {
            if (user.getId() != me.getId()) {  // Không hiển thị chính mình
                userListModel.addElement(user);
            }
        }
    }

    private void receiveTextMessage(MessageDTO msg) {
        // Giải mã nội dung
        String plaintext = connection.decryptMessage(msg.getEncryptedContent());
        boolean isMe = msg.getSenderId() == me.getId();

        // Hiển thị bubble
        addMessageBubble(msg.getSenderUsername(), plaintext, isMe, msg.getTimestamp(), false);

        // Lưu local history
        historyDB.saveMessage(
            msg.getRoomId(), msg.getSenderId(),
            msg.getSenderUsername(), plaintext, "TEXT", null,
            msg.getTimestamp(), isMe
        );
    }

    private void receiveFileMessage(MessageDTO msg) {
        boolean isMe = msg.getSenderId() == me.getId();
        String display = "📎 File: " + msg.getFileName() +
                " (" + formatFileSize(msg.getFileSize()) + ")";
        addMessageBubble(msg.getSenderUsername(), display, isMe, msg.getTimestamp(), true);

        // Hỏi user có muốn tải không (nếu không phải mình gửi)
        if (!isMe) {
            int choice = JOptionPane.showConfirmDialog(this,
                    "Nhận file: " + msg.getFileName() + "\nBạn có muốn lưu?",
                    "File nhận được", JOptionPane.YES_NO_OPTION);
            if (choice == JOptionPane.YES_OPTION) {
                saveReceivedFile(msg);
            }
        }
    }

    private void saveReceivedFile(MessageDTO msg) {
        JFileChooser chooser = new JFileChooser();
        chooser.setSelectedFile(new File(msg.getFileName()));
        if (chooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            SwingWorker<Void, Void> worker = new SwingWorker<>() {
                @Override protected Void doInBackground() throws Exception {
                    byte[] decrypted = connection.decryptFile(msg.getEncryptedContent());
                    if (decrypted != null) {
                        Files.write(chooser.getSelectedFile().toPath(), decrypted);
                    }
                    return null;
                }
                @Override protected void done() {
                    addSystemMessage("✅ Đã lưu file: " + chooser.getSelectedFile().getName());
                }
            };
            worker.execute();
        }
    }

    // ────────────────────────────────────────────────────────────
    // UI helpers – Message bubbles
    // ────────────────────────────────────────────────────────────

    private void addMessageBubble(String sender, String content,
                                   boolean isMe, long timestamp, boolean isFile) {
        JPanel bubble = new JPanel(new BorderLayout(0, 4));
        bubble.setBackground(BG_CHAT);
        bubble.setMaximumSize(new Dimension(700, Integer.MAX_VALUE));
        bubble.setBorder(new EmptyBorder(4, 0, 4, 0));
        bubble.setAlignmentX(isMe ? Component.RIGHT_ALIGNMENT : Component.LEFT_ALIGNMENT);

        // Content bubble
        JPanel msgBubble = new JPanel() {
            @Override protected void paintComponent(Graphics g) {
                Graphics2D g2 = (Graphics2D) g;
                g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
                g2.setColor(isMe ? BG_MSG_ME : BG_MSG_OTHER);
                g2.fillRoundRect(0, 0, getWidth(), getHeight(), 16, 16);
            }
        };
        msgBubble.setLayout(new BorderLayout(0, 4));
        msgBubble.setOpaque(false);
        msgBubble.setBorder(new EmptyBorder(10, 14, 10, 14));

        JLabel lblContent = new JLabel("<html><body style='width:280px'>" +
                escapeHtml(content) + "</body></html>");
        lblContent.setFont(new Font("Segoe UI", Font.PLAIN, 14));
        lblContent.setForeground(isMe ? Color.WHITE : TEXT_PRIMARY);

        String timeStr = new SimpleDateFormat("HH:mm").format(new Date(timestamp));
        JLabel lblMeta = new JLabel(
                (isMe ? "" : sender + " · ") + timeStr +
                (isFile ? " 📎" : " 🔒"));
        lblMeta.setFont(new Font("Segoe UI", Font.PLAIN, 10));
        lblMeta.setForeground(isMe ? new Color(200, 200, 255) : TEXT_SECOND);

        msgBubble.add(lblContent, BorderLayout.CENTER);
        msgBubble.add(lblMeta,    BorderLayout.SOUTH);

        // Align wrapper
        JPanel wrapper = new JPanel(new FlowLayout(isMe ? FlowLayout.RIGHT : FlowLayout.LEFT, 0, 0));
        wrapper.setBackground(BG_CHAT);
        wrapper.setMaximumSize(new Dimension(Integer.MAX_VALUE, Integer.MAX_VALUE));
        wrapper.add(msgBubble);

        chatPanel.add(wrapper);
        chatPanel.add(Box.createVerticalStrut(4));
        chatPanel.revalidate();
        chatPanel.repaint();

        // Auto-scroll to bottom
        SwingUtilities.invokeLater(() -> {
            JScrollBar sb = chatScrollPane.getVerticalScrollBar();
            sb.setValue(sb.getMaximum());
        });
    }

    private void addSystemMessage(String text) {
        JLabel lbl = new JLabel(text, SwingConstants.CENTER);
        lbl.setFont(new Font("Segoe UI", Font.ITALIC, 12));
        lbl.setForeground(TEXT_SECOND);
        lbl.setAlignmentX(Component.CENTER_ALIGNMENT);
        lbl.setBorder(new EmptyBorder(8, 0, 8, 0));

        chatPanel.add(lbl);
        chatPanel.add(Box.createVerticalStrut(2));
        chatPanel.revalidate();
    }

    // ────────────────────────────────────────────────────────────
    // Event listeners
    // ────────────────────────────────────────────────────────────

    private void setupListeners() {
        // Chọn user để chat
        userList.addListSelectionListener(e -> {
            if (!e.getValueIsAdjusting()) {
                UserDTO selected = userList.getSelectedValue();
                if (selected != null) {
                    openPrivateChat(selected);
                }
            }
        });

        // Gửi tin nhắn
        btnSend.addActionListener(e -> sendMessage());
        txtInput.addKeyListener(new KeyAdapter() {
            @Override public void keyPressed(KeyEvent e) {
                if (e.getKeyCode() == KeyEvent.VK_ENTER && !e.isShiftDown()) {
                    sendMessage();
                }
            }
        });

        // Gửi file
        btnFile.addActionListener(e -> sendFile());

        // Lịch sử chat
        btnHistory.addActionListener(e -> showHistory());
    }

    private void openPrivateChat(UserDTO target) {
        this.selectedUser = target;
        // Virtual room ID
        int myId = me.getId();
        int targetId = target.getId();
        this.currentRoomId = Math.min(myId, targetId) * 100_000 + Math.max(myId, targetId);

        lblChatWith.setText(target.getDisplayName() != null ?
                target.getDisplayName() : target.getUsername());
        txtInput.setEnabled(true);
        btnSend.setEnabled(true);
        txtInput.requestFocus();

        // Xoá chat panel và load lịch sử local
        chatPanel.removeAll();
        addSystemMessage("🔒 Chat mã hoá với " +
                (target.getDisplayName() != null ? target.getDisplayName() : target.getUsername()));

        List<Object[]> history = historyDB.getHistory(currentRoomId, 50);
        for (Object[] row : history) {
            String senderName = (String) row[2];
            String content    = (String) row[3];
            boolean isSent    = (boolean) row[7];
            long ts           = (long) row[6];
            addMessageBubble(senderName, content, isSent, ts, "FILE".equals(row[4]));
        }

        chatPanel.revalidate();
        chatPanel.repaint();
    }

    private void sendMessage() {
        if (selectedUser == null) return;
        String text = txtInput.getText().trim();
        if (text.isEmpty()) return;

        try {
            connection.sendText(currentRoomId, text);

            // Hiển thị ngay (optimistic UI)
            addMessageBubble(me.getUsername(), text, true,
                    System.currentTimeMillis(), false);

            // Lưu local history
            historyDB.saveMessage(currentRoomId, me.getId(), me.getUsername(),
                    text, "TEXT", null, System.currentTimeMillis(), true);

            txtInput.setText("");
        } catch (Exception ex) {
            log.error("Lỗi gửi tin nhắn: {}", ex.getMessage());
            addSystemMessage("❌ Gửi thất bại: " + ex.getMessage());
        }
    }

    private void sendFile() {
        if (selectedUser == null) {
            JOptionPane.showMessageDialog(this,
                    "Hãy chọn người dùng trước!", "Thông báo",
                    JOptionPane.INFORMATION_MESSAGE);
            return;
        }

        JFileChooser chooser = new JFileChooser();
        if (chooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            File file = chooser.getSelectedFile();
            if (file.length() > 50 * 1024 * 1024) { // Giới hạn 50MB
                JOptionPane.showMessageDialog(this,
                        "File quá lớn (max 50MB)!", "Lỗi",
                        JOptionPane.ERROR_MESSAGE);
                return;
            }

            SwingWorker<Void, Void> worker = new SwingWorker<>() {
                @Override protected Void doInBackground() throws Exception {
                    byte[] bytes = Files.readAllBytes(file.toPath());
                    connection.sendFile(currentRoomId, file.getName(), bytes);
                    return null;
                }
                @Override protected void done() {
                    addMessageBubble(me.getUsername(),
                            "📎 File: " + file.getName() + " (" +
                            formatFileSize(file.length()) + ")",
                            true, System.currentTimeMillis(), true);
                    historyDB.saveMessage(currentRoomId, me.getId(), me.getUsername(),
                            "[File: " + file.getName() + "]", "FILE", file.getName(),
                            System.currentTimeMillis(), true);
                }
            };
            addSystemMessage("⏳ Đang mã hoá và gửi: " + file.getName() + "...");
            worker.execute();
        }
    }

    private void showHistory() {
        HistoryPanel historyPanel = new HistoryPanel(historyDB);
        JDialog dialog = new JDialog(this, "Lịch Sử Chat", true);
        dialog.setContentPane(historyPanel);
        dialog.setSize(600, 500);
        dialog.setLocationRelativeTo(this);
        dialog.setVisible(true);
    }

    // ────────────────────────────────────────────────────────────
    // Utils
    // ────────────────────────────────────────────────────────────

    private String formatFileSize(long bytes) {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return String.format("%.1f KB", bytes / 1024.0);
        return String.format("%.1f MB", bytes / (1024.0 * 1024));
    }

    private String escapeHtml(String text) {
        return text.replace("&", "&amp;")
                   .replace("<", "&lt;")
                   .replace(">", "&gt;")
                   .replace("\n", "<br>");
    }

    // ────────────────────────────────────────────────────────────
    // UserList Cell Renderer
    // ────────────────────────────────────────────────────────────

    private class UserListCellRenderer extends DefaultListCellRenderer {
        @Override
        public Component getListCellRendererComponent(JList<?> list, Object value,
                int index, boolean isSelected, boolean cellHasFocus) {
            super.getListCellRendererComponent(list, value, index, isSelected, cellHasFocus);

            if (value instanceof UserDTO user) {
                JPanel panel = new JPanel(new BorderLayout(10, 0));
                panel.setBackground(isSelected ? BG_HOVER : BG_SIDEBAR);
                panel.setBorder(new EmptyBorder(8, 14, 8, 14));

                // Avatar circle
                JLabel avatar = new JLabel("👤");
                avatar.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 26));

                // Info
                JPanel info = new JPanel(new GridLayout(2, 1, 0, 2));
                info.setBackground(panel.getBackground());

                JLabel name = new JLabel(user.getDisplayName() != null ?
                        user.getDisplayName() : user.getUsername());
                name.setFont(new Font("Segoe UI", Font.BOLD, 13));
                name.setForeground(TEXT_PRIMARY);

                boolean online = "ONLINE".equals(user.getStatus());
                JLabel status = new JLabel((online ? "🟢 " : "⚫ ") +
                        (online ? "Online" : "Offline"));
                status.setFont(new Font("Segoe UI Emoji", Font.PLAIN, 11));
                status.setForeground(online ? ONLINE_DOT : TEXT_SECOND);

                info.add(name);
                info.add(status);

                panel.add(avatar, BorderLayout.WEST);
                panel.add(info,   BorderLayout.CENTER);

                return panel;
            }
            return this;
        }
    }
}

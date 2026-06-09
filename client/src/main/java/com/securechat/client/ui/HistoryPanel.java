package com.securechat.client.ui;

import com.securechat.client.storage.LocalHistoryDB;

import javax.swing.*;
import javax.swing.border.EmptyBorder;
import java.awt.*;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;

/**
 * HistoryPanel – Xem lịch sử chat từ SQLite local.
 *
 * Tính năng:
 * - Tìm kiếm theo từ khoá
 * - Hiển thị lịch sử theo định dạng dễ đọc
 * - Mở trong Dialog từ MainChatFrame
 */
public class HistoryPanel extends JPanel {

    private static final Color BG_MAIN    = new Color(18, 18, 30);
    private static final Color BG_ITEM    = new Color(28, 28, 45);
    private static final Color BG_INPUT   = new Color(38, 38, 58);
    private static final Color ACCENT     = new Color(99, 102, 241);
    private static final Color TEXT_PRIM  = new Color(248, 250, 252);
    private static final Color TEXT_SEC   = new Color(148, 163, 184);
    private static final Color DIVIDER    = new Color(40, 40, 65);

    private final LocalHistoryDB historyDB;
    private JTextField           txtSearch;
    private JPanel               resultPanel;
    private JScrollPane          scrollPane;

    public HistoryPanel(LocalHistoryDB historyDB) {
        this.historyDB = historyDB;
        setLayout(new BorderLayout(0, 0));
        setBackground(BG_MAIN);
        initComponents();
        loadAllHistory();
    }

    private void initComponents() {
        // ── Header ──────────────────────────────────────────────
        JPanel header = new JPanel(new BorderLayout(10, 0));
        header.setBackground(new Color(22, 22, 38));
        header.setBorder(new EmptyBorder(16, 20, 16, 20));

        JLabel lblTitle = new JLabel("📋 Lịch Sử Chat (Local SQLite)");
        lblTitle.setFont(new Font("Segoe UI", Font.BOLD, 16));
        lblTitle.setForeground(TEXT_PRIM);

        // Search bar
        JPanel searchBar = new JPanel(new BorderLayout(8, 0));
        searchBar.setBackground(new Color(22, 22, 38));

        txtSearch = new JTextField();
        txtSearch.setBackground(BG_INPUT);
        txtSearch.setForeground(TEXT_PRIM);
        txtSearch.setCaretColor(TEXT_PRIM);
        txtSearch.setFont(new Font("Segoe UI", Font.PLAIN, 13));
        txtSearch.setBorder(BorderFactory.createCompoundBorder(
                BorderFactory.createLineBorder(new Color(60, 60, 90), 1),
                new EmptyBorder(7, 10, 7, 10)));
        txtSearch.putClientProperty("JTextField.placeholderText", "Tìm kiếm tin nhắn...");

        JButton btnSearch = new JButton("🔍");
        btnSearch.setBackground(ACCENT);
        btnSearch.setForeground(Color.WHITE);
        btnSearch.setBorderPainted(false);
        btnSearch.setFocusPainted(false);
        btnSearch.setCursor(new Cursor(Cursor.HAND_CURSOR));
        btnSearch.setPreferredSize(new Dimension(44, 36));

        btnSearch.addActionListener(e -> performSearch());
        txtSearch.addActionListener(e -> performSearch());

        searchBar.add(txtSearch, BorderLayout.CENTER);
        searchBar.add(btnSearch, BorderLayout.EAST);

        header.add(lblTitle,   BorderLayout.NORTH);
        header.add(searchBar,  BorderLayout.SOUTH);

        // ── Results ─────────────────────────────────────────────
        resultPanel = new JPanel();
        resultPanel.setLayout(new BoxLayout(resultPanel, BoxLayout.Y_AXIS));
        resultPanel.setBackground(BG_MAIN);
        resultPanel.setBorder(new EmptyBorder(10, 16, 10, 16));

        scrollPane = new JScrollPane(resultPanel);
        scrollPane.setBorder(null);
        scrollPane.getViewport().setBackground(BG_MAIN);

        // ── Footer ──────────────────────────────────────────────
        JPanel footer = new JPanel(new FlowLayout(FlowLayout.LEFT));
        footer.setBackground(new Color(15, 15, 25));
        footer.setBorder(new EmptyBorder(6, 16, 6, 16));

        JLabel lblNote = new JLabel("💡 Lịch sử được lưu local và mã hoá khi truyền. " +
                "Chỉ bạn mới đọc được.");
        lblNote.setFont(new Font("Segoe UI", Font.ITALIC, 11));
        lblNote.setForeground(TEXT_SEC);
        footer.add(lblNote);

        add(header,    BorderLayout.NORTH);
        add(scrollPane, BorderLayout.CENTER);
        add(footer,    BorderLayout.SOUTH);
    }

    private void loadAllHistory() {
        // Hiển thị 100 tin nhắn gần nhất từ tất cả rooms
        List<Object[]> all = historyDB.getHistory(0, 100);
        displayResults(all, "Tất cả lịch sử");
    }

    private void performSearch() {
        String keyword = txtSearch.getText().trim();
        if (keyword.isEmpty()) {
            loadAllHistory();
            return;
        }
        List<Object[]> results = historyDB.searchMessages(keyword);
        displayResults(results, "Kết quả tìm: \"" + keyword + "\"");
    }

    private void displayResults(List<Object[]> rows, String sectionTitle) {
        resultPanel.removeAll();

        // Section title
        JLabel lblSection = new JLabel(sectionTitle + " (" + rows.size() + " tin)");
        lblSection.setFont(new Font("Segoe UI", Font.BOLD, 12));
        lblSection.setForeground(TEXT_SEC);
        lblSection.setBorder(new EmptyBorder(0, 0, 10, 0));
        lblSection.setAlignmentX(Component.LEFT_ALIGNMENT);
        resultPanel.add(lblSection);

        if (rows.isEmpty()) {
            JLabel empty = new JLabel("Không có tin nhắn nào.");
            empty.setFont(new Font("Segoe UI", Font.ITALIC, 13));
            empty.setForeground(TEXT_SEC);
            empty.setAlignmentX(Component.CENTER_ALIGNMENT);
            empty.setBorder(new EmptyBorder(40, 0, 0, 0));
            resultPanel.add(empty);
        }

        SimpleDateFormat sdf = new SimpleDateFormat("dd/MM/yyyy HH:mm");

        for (Object[] row : rows) {
            // row: [roomId, senderId, senderName, content, type, fileName, timestamp, isSent]
            String sender    = row.length > 2 ? (String) row[2] : "?";
            String content   = row.length > 3 ? (String) row[3] : "";
            String type      = row.length > 4 ? (String) row[4] : "TEXT";
            long   timestamp = row.length > 6 ? ((Number) row[6]).longValue() : 0;
            boolean isSent   = row.length > 7 && (boolean) row[7];

            JPanel item = new JPanel(new BorderLayout(10, 2));
            item.setBackground(BG_ITEM);
            item.setBorder(BorderFactory.createCompoundBorder(
                    BorderFactory.createLineBorder(DIVIDER, 1),
                    new EmptyBorder(10, 14, 10, 14)));
            item.setMaximumSize(new Dimension(Integer.MAX_VALUE, 70));
            item.setAlignmentX(Component.LEFT_ALIGNMENT);

            // Top: sender + time
            JPanel top = new JPanel(new BorderLayout());
            top.setBackground(BG_ITEM);

            JLabel lblSender = new JLabel((isSent ? "➤ Bạn" : "← " + sender));
            lblSender.setFont(new Font("Segoe UI", Font.BOLD, 12));
            lblSender.setForeground(isSent ? ACCENT : TEXT_PRIM);

            JLabel lblTime = new JLabel(timestamp > 0 ? sdf.format(new Date(timestamp)) : "");
            lblTime.setFont(new Font("Segoe UI", Font.PLAIN, 11));
            lblTime.setForeground(TEXT_SEC);

            top.add(lblSender, BorderLayout.WEST);
            top.add(lblTime,   BorderLayout.EAST);

            // Content
            String displayContent = "FILE".equals(type)
                    ? "📎 " + content
                    : content;
            JLabel lblContent = new JLabel("<html><body style='width:480px'>" +
                    escapeHtml(displayContent) + "</body></html>");
            lblContent.setFont(new Font("Segoe UI", Font.PLAIN, 13));
            lblContent.setForeground(TEXT_PRIM);

            item.add(top,        BorderLayout.NORTH);
            item.add(lblContent, BorderLayout.CENTER);

            resultPanel.add(item);
            resultPanel.add(Box.createVerticalStrut(6));
        }

        resultPanel.revalidate();
        resultPanel.repaint();

        // Scroll to top
        SwingUtilities.invokeLater(() ->
                scrollPane.getVerticalScrollBar().setValue(0));
    }

    private String escapeHtml(String text) {
        if (text == null) return "";
        return text.replace("&", "&amp;")
                   .replace("<", "&lt;")
                   .replace(">", "&gt;");
    }
}

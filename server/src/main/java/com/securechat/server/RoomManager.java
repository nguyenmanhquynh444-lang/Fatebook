package com.securechat.server;

import com.securechat.common.dto.MessageDTO;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * RoomManager – Quản lý phòng chat và routing tin nhắn.
 *
 * - Lưu danh sách client đang kết nối (userId → ClientHandler)
 * - Lưu danh sách thành viên mỗi phòng (roomId → Set<userId>)
 * - Broadcast tin nhắn đến đúng người nhận
 */
public class RoomManager {

    private static final Logger log = LoggerFactory.getLogger(RoomManager.class);

    /** userId → ClientHandler (tất cả client đang online) */
    private final Map<Integer, ClientHandler> onlineClients =
            new ConcurrentHashMap<>();

    /** roomId → Set<userId> (thành viên mỗi phòng) */
    private final Map<Integer, Set<Integer>> roomMembers =
            new ConcurrentHashMap<>();

    // ────────────────────────────────────────────────────────────
    // Quản lý client
    // ────────────────────────────────────────────────────────────

    public void registerClient(int userId, ClientHandler handler) {
        onlineClients.put(userId, handler);
        log.info("Client đăng ký: userId={}, tổng online={}",
                userId, onlineClients.size());
    }

    public void removeClient(int userId) {
        onlineClients.remove(userId);
        // Xoá khỏi tất cả các phòng
        roomMembers.values().forEach(members -> members.remove(userId));
        log.info("Client rời: userId={}, tổng online={}",
                userId, onlineClients.size());
    }

    public boolean isOnline(int userId) {
        return onlineClients.containsKey(userId);
    }

    public Collection<ClientHandler> getOnlineClients() {
        return onlineClients.values();
    }

    // ────────────────────────────────────────────────────────────
    // Quản lý phòng
    // ────────────────────────────────────────────────────────────

    public void joinRoom(int roomId, int userId, ClientHandler handler) {
        roomMembers.computeIfAbsent(roomId, k -> ConcurrentHashMap.newKeySet())
                   .add(userId);
        onlineClients.put(userId, handler);
    }

    public void leaveRoom(int roomId, int userId) {
        Set<Integer> members = roomMembers.get(roomId);
        if (members != null) {
            members.remove(userId);
        }
    }

    public Set<Integer> getRoomMembers(int roomId) {
        return roomMembers.getOrDefault(roomId, Collections.emptySet());
    }

    /**
     * Tạo hoặc lấy phòng chat riêng tư giữa 2 người.
     * RoomId = min(a,b)*100000 + max(a,b) (đảm bảo duy nhất)
     */
    public int getOrCreatePrivateRoom(int userId1, int userId2) {
        int minId = Math.min(userId1, userId2);
        int maxId = Math.max(userId1, userId2);
        int virtualRoomId = minId * 100_000 + maxId;

        roomMembers.computeIfAbsent(virtualRoomId, k -> {
            Set<Integer> members = ConcurrentHashMap.newKeySet();
            members.add(userId1);
            members.add(userId2);
            return members;
        });

        return virtualRoomId;
    }

    // ────────────────────────────────────────────────────────────
    // Broadcast
    // ────────────────────────────────────────────────────────────

    /**
     * Gửi tin nhắn đến tất cả thành viên trong phòng (trừ người gửi).
     */
    public void broadcastToRoom(int roomId, MessageDTO msg, int excludeUserId) {
        Set<Integer> members = getRoomMembers(roomId);
        if (members.isEmpty()) {
            // Nếu không có ai trong phòng, gửi trực tiếp đến receiver
            // (với private chat roomId đã encode cả hai userId)
            broadcastToAll(msg, excludeUserId);
            return;
        }

        int count = 0;
        for (int memberId : members) {
            if (memberId == excludeUserId) continue;
            ClientHandler handler = onlineClients.get(memberId);
            if (handler != null) {
                handler.sendMessage(msg);
                count++;
            }
        }
        log.debug("Broadcast roomId={} → {} clients", roomId, count);
    }

    /**
     * Gửi tin nhắn đến tất cả client đang online (trừ người gửi).
     * Dùng cho SYSTEM messages (user joined/left).
     */
    public void broadcastToAll(MessageDTO msg, int excludeUserId) {
        int count = 0;
        for (Map.Entry<Integer, ClientHandler> entry : onlineClients.entrySet()) {
            if (entry.getKey() == excludeUserId) continue;
            entry.getValue().sendMessage(msg);
            count++;
        }
        log.debug("Broadcast ALL → {} clients", count);
    }

    /**
     * Gửi tin nhắn trực tiếp đến một user cụ thể.
     */
    public boolean sendToUser(int targetUserId, MessageDTO msg) {
        ClientHandler handler = onlineClients.get(targetUserId);
        if (handler != null) {
            handler.sendMessage(msg);
            return true;
        }
        return false; // User offline
    }
}

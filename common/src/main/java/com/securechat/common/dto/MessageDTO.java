package com.securechat.common.dto;

import java.io.Serializable;

/**
 * MessageDTO – Gói tin nhắn/file truyền qua mạng.
 * Nội dung đã được mã hoá AES-GCM.
 */
public class MessageDTO implements Serializable {
    private static final long serialVersionUID = 2L;

    public enum Type { TEXT, FILE, SYSTEM, LOGIN, LOGIN_RESPONSE,
                       KEY_EXCHANGE, USER_LIST, JOIN_ROOM, LEAVE_ROOM,
                       TYPING, READ_RECEIPT }

    private Type   type;
    private int    senderId;
    private String senderUsername;
    private int    roomId;
    private String encryptedContent;   // AES-GCM encrypted, Base64
    private String plainContent;       // chỉ dùng cho SYSTEM/LOGIN (không mã hoá)
    private String fileName;           // nếu type=FILE
    private long   fileSize;
    private long   timestamp;

    public MessageDTO() {
        this.timestamp = System.currentTimeMillis();
    }

    public MessageDTO(Type type) {
        this();
        this.type = type;
    }

    // ── Static factory methods ──────────────────────────────────

    public static MessageDTO loginRequest(String username, String password) {
        MessageDTO msg = new MessageDTO(Type.LOGIN);
        msg.plainContent = username + ":" + password;
        return msg;
    }

    public static MessageDTO textMessage(int senderId, String senderUsername,
                                          int roomId, String encryptedContent) {
        MessageDTO msg = new MessageDTO(Type.TEXT);
        msg.senderId        = senderId;
        msg.senderUsername  = senderUsername;
        msg.roomId          = roomId;
        msg.encryptedContent = encryptedContent;
        return msg;
    }

    public static MessageDTO fileMessage(int senderId, String senderUsername,
                                          int roomId, String encryptedContent,
                                          String fileName, long fileSize) {
        MessageDTO msg = new MessageDTO(Type.FILE);
        msg.senderId         = senderId;
        msg.senderUsername   = senderUsername;
        msg.roomId           = roomId;
        msg.encryptedContent = encryptedContent;
        msg.fileName         = fileName;
        msg.fileSize         = fileSize;
        return msg;
    }

    public static MessageDTO systemMessage(String content) {
        MessageDTO msg = new MessageDTO(Type.SYSTEM);
        msg.plainContent = content;
        return msg;
    }

    // ── Getters & Setters ──────────────────────────────────────

    public Type   getType()                     { return type; }
    public void   setType(Type t)               { this.type = t; }

    public int    getSenderId()                  { return senderId; }
    public void   setSenderId(int id)            { this.senderId = id; }

    public String getSenderUsername()            { return senderUsername; }
    public void   setSenderUsername(String u)    { this.senderUsername = u; }

    public int    getRoomId()                    { return roomId; }
    public void   setRoomId(int id)              { this.roomId = id; }

    public String getEncryptedContent()          { return encryptedContent; }
    public void   setEncryptedContent(String c)  { this.encryptedContent = c; }

    public String getPlainContent()              { return plainContent; }
    public void   setPlainContent(String c)      { this.plainContent = c; }

    public String getFileName()                  { return fileName; }
    public void   setFileName(String fn)         { this.fileName = fn; }

    public long   getFileSize()                  { return fileSize; }
    public void   setFileSize(long fs)           { this.fileSize = fs; }

    public long   getTimestamp()                 { return timestamp; }
    public void   setTimestamp(long ts)          { this.timestamp = ts; }
}

package com.securechat.common.dto;

import java.io.Serializable;

/**
 * UserDTO – Data Transfer Object truyền thông tin người dùng qua mạng.
 * Không chứa password.
 */
public class UserDTO implements Serializable {
    private static final long serialVersionUID = 1L;

    private int    id;
    private String username;
    private String displayName;
    private String publicKey;   // RSA Public Key (Base64)
    private String status;      // ONLINE / OFFLINE / AWAY
    private String sessionToken;

    public UserDTO() {}

    public UserDTO(int id, String username, String displayName,
                   String publicKey, String status) {
        this.id          = id;
        this.username    = username;
        this.displayName = displayName;
        this.publicKey   = publicKey;
        this.status      = status;
    }

    // ── Getters & Setters ──────────────────────────────────────

    public int    getId()           { return id; }
    public void   setId(int id)     { this.id = id; }

    public String getUsername()              { return username; }
    public void   setUsername(String u)      { this.username = u; }

    public String getDisplayName()           { return displayName; }
    public void   setDisplayName(String dn)  { this.displayName = dn; }

    public String getPublicKey()             { return publicKey; }
    public void   setPublicKey(String pk)    { this.publicKey = pk; }

    public String getStatus()                { return status; }
    public void   setStatus(String s)        { this.status = s; }

    public String getSessionToken()          { return sessionToken; }
    public void   setSessionToken(String t)  { this.sessionToken = t; }

    @Override
    public String toString() {
        return "UserDTO{id=" + id + ", username='" + username
                + "', displayName='" + displayName + "', status='" + status + "'}";
    }
}

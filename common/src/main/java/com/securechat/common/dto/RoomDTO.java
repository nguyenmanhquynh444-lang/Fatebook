package com.securechat.common.dto;

import java.io.Serializable;
import java.util.List;

/**
 * RoomDTO – Thông tin phòng chat truyền qua mạng.
 */
public class RoomDTO implements Serializable {
    private static final long serialVersionUID = 3L;

    private int          id;
    private String       roomName;
    private String       roomType;   // PRIVATE / GROUP
    private List<UserDTO> members;
    private int          createdBy;
    private long         createdAt;

    public RoomDTO() {}

    public RoomDTO(int id, String roomName, String roomType) {
        this.id       = id;
        this.roomName = roomName;
        this.roomType = roomType;
    }

    public int          getId()                    { return id; }
    public void         setId(int id)              { this.id = id; }

    public String       getRoomName()              { return roomName; }
    public void         setRoomName(String n)      { this.roomName = n; }

    public String       getRoomType()              { return roomType; }
    public void         setRoomType(String t)      { this.roomType = t; }

    public List<UserDTO> getMembers()              { return members; }
    public void          setMembers(List<UserDTO> m) { this.members = m; }

    public int          getCreatedBy()             { return createdBy; }
    public void         setCreatedBy(int cb)       { this.createdBy = cb; }

    public long         getCreatedAt()             { return createdAt; }
    public void         setCreatedAt(long ca)      { this.createdAt = ca; }
}

using System.Collections.Generic;

namespace SecureChat.Common.DTO
{
    public class RoomDTO
    {
        public int Id { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string RoomType { get; set; } = "PRIVATE"; // PRIVATE / GROUP
        public List<UserDTO> Members { get; set; } = new List<UserDTO>();
        public int CreatedBy { get; set; }
        public long CreatedAt { get; set; }

        public RoomDTO() { }

        public RoomDTO(int id, string roomName, string roomType)
        {
            Id = id;
            RoomName = roomName;
            RoomType = roomType;
        }
    }
}

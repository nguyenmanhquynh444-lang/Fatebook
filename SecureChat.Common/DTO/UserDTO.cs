namespace SecureChat.Common.DTO
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty; // RSA Public Key (Base64)
        public string Status { get; set; } = "OFFLINE";      // ONLINE / OFFLINE / AWAY
        public string SessionToken { get; set; } = string.Empty;
        public string AvatarBase64 { get; set; } = string.Empty; // Avatar Base64
        public string Role { get; set; } = "USER";           // ADMIN / USER
        public bool IsActive { get; set; } = true;
        public string FriendshipStatus { get; set; } = "NONE"; // NONE / PENDING_SENT / PENDING_RECEIVED / ACCEPTED

        public override string ToString()
        {
            return $"UserDTO{{Id={Id}, Username='{Username}', DisplayName='{DisplayName}', Role='{Role}', Status='{Status}', IsActive={IsActive}}}";
        }
    }
}

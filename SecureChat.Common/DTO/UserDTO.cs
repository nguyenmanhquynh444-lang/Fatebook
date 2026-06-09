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

        public override string ToString()
        {
            return $"UserDTO{{Id={Id}, Username='{Username}', DisplayName='{DisplayName}', Status='{Status}'}}";
        }
    }
}

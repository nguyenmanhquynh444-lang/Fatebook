using System;

namespace SecureChat.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var server = new Server();
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] Lỗi nghiêm trọng: {ex.Message}");
                Console.WriteLine("Nhấn phím bất kỳ để thoát...");
                Console.ReadKey();
            }
        }
    }
}

# Secure Chat System
## Hệ Thống Chat Bảo Mật C# (Windows Forms & .NET 8.0 Console)
### Môn: Lập Trình Mạng – VHU

Hệ thống Chat bảo mật gồm hai ứng dụng chạy trên .NET 8.0, được cấu trúc dưới dạng **hai Solution Visual Studio riêng biệt** cùng chia sẻ thư viện DTO/Crypto chung:
1. **SecureChat.Server.slnx** (Server Console App + MySQL)
2. **SecureChat.Client.slnx** (Client Windows Forms App + SQLite)

---

## Cấu Trúc Dự Án

```
Fatebook/
├── SecureChat.Server.slnx           # Solution cho phía Server (Server + Common)
├── SecureChat.Client.slnx           # Solution cho phía Client (Client + Common)
│
├── SecureChat.Common/               # Dự án dùng chung: DTO và Thư viện mã hóa
│   ├── Crypto/
│   │   ├── RSAUtil.cs               # RSA-2048 OAEP-SHA256
│   │   └── AESUtil.cs               # AES-256-GCM
│   └── DTO/
│       ├── UserDTO.cs
│       ├── MessageDTO.cs
│       └── RoomDTO.cs
│
├── SecureChat.Server/               # Server Console App
│   ├── Server.cs                    # Xử lý SslStream trên cổng 8443
│   ├── FileTransferHandler.cs       # Nhận và truyền file trên cổng 8444
│   ├── Crypto/CertGenerator.cs      # Tự động tạo CA & Server SSL (PFX)
│   └── DAO/                         # Tương tác MySQL qua MySqlConnector
│
├── SecureChat.Client/               # Client Windows Forms App
│   ├── SecureConnection.cs          # SslStream client kết nối TLS & Validate CA
│   ├── Storage/LocalHistoryDB.cs    # Lưu lịch sử chat offline bằng SQLite
│   └── Forms/                       # Các màn hình LoginForm, MainChatForm, HistoryForm
```

---

## Hướng Dẫn Cài Đặt & Chạy

### Bước 1: Yêu Cầu Hệ Thống
- **.NET 8.0 SDK** hoặc mới hơn (Kiểm tra bằng cách chạy `dotnet --version` trong cmd).
- **XAMPP** (Đảm bảo Apache và MySQL đã được bật).
- **Visual Studio 2022** (Khuyên dùng) hoặc VS Code.

---

### Bước 2: Tạo Cơ Sở Dữ Liệu (XAMPP MySQL)
1. Mở **XAMPP Control Panel** → Start **MySQL**.
2. Nhấn nút **Admin** ở dòng MySQL hoặc truy cập vào trình duyệt: [http://localhost/phpmyadmin](http://localhost/phpmyadmin).
3. Import file [database/schema.sql](file:///c:/Users/Admin/Documents/VHU/ltm/Fatebook/database/schema.sql) để tự động tạo database `secure_chat` và các tài khoản mẫu.
   * Hoặc qua Command Prompt (CMD):
     ```cmd
     C:\xampp\mysql\bin\mysql.exe -u root < c:\Users\Admin\Documents\VHU\ltm\Fatebook\database\schema.sql
     ```

**Danh sách tài khoản mẫu** (mật khẩu mặc định cho tất cả: `admin123`):
* `admin`
* `kemchui`
* `shinichi`
* `Quynh`

---

### Bước 3: Khởi Chạy Server (Tự Động Sinh SSL Certificate)
Bạn có thể mở solution `SecureChat.Server.slnx` bằng Visual Studio 2022 và bấm **Run**, hoặc chạy trực tiếp bằng dòng lệnh:

```cmd
dotnet run --project SecureChat.Server/SecureChat.Server.csproj
```

* **Lưu ý:** Khi chạy lần đầu, Server sẽ tự động kiểm tra và sinh chứng chỉ SSL tự ký:
  - Cập nhật Root CA vào thư mục `certs/ca.crt`
  - Cập nhật chứng chỉ máy chủ `certs/server.pfx` (được ký trực tiếp từ CA trên)
* Server sẽ lắng nghe tại các cổng:
  - **Port 8443** (TLS 1.3 / 1.2) - Kênh truyền nhắn tin.
  - **Port 8444** (TLS 1.3 / 1.2) - Kênh truyền tệp tin.

---

### Bước 4: Khởi Chạy Client (Windows Forms)
Mở solution `SecureChat.Client.slnx` bằng Visual Studio 2022 và bấm **Run**, hoặc chạy trực tiếp bằng dòng lệnh:

```cmd
dotnet run --project SecureChat.Client/SecureChat.Client.csproj
```

* **Kết nối:** 
  - Địa chỉ Server: `127.0.0.1` hoặc `localhost` (nếu chạy cùng máy), hoặc địa chỉ IP LAN của máy Server.
  - Cổng: `8443`
  - Nhập tài khoản mẫu (ví dụ: `admin` / `admin123`) để bắt đầu chat.
  - Client sẽ tự động đọc file `certs/ca.crt` để xác thực chứng chỉ an toàn của máy chủ mà không cần cài đặt CA vào hệ điều hành.

---

## Kiến Trúc Bảo Mật 3 Lớp

```
[1] SSL/TLS 1.3/1.2 → Bảo mật kênh truyền toàn vẹn (chống MITM)
[2] RSA-2048 OAEP   → Sử dụng mã hóa khóa công khai trao đổi khóa phiên AES
[3] AES-256-GCM     → Mã hóa tin nhắn và file trực tiếp đầu cuối (end-to-end)
```

1. **SSL Handshake**: Client kết nối tới Server cổng 8443. Server gửi chứng chỉ SSL máy chủ cho Client. Client dùng file `ca.crt` tự động kiểm tra chữ ký CA. Nếu hợp lệ, kênh truyền TLS được thiết lập.
2. **Đăng nhập**: Thông tin đăng nhập truyền an toàn qua TLS. Server dùng `BCrypt` để kiểm tra với CSDL MySQL của XAMPP.
3. **Trao đổi Khóa RSA/AES**:
   - Server gửi khóa công khai RSA tạm thời (mỗi phiên sinh mới) cho Client.
   - Client gửi khóa công khai RSA của Client lên Server.
   - Client sinh ngẫu nhiên khóa phiên AES-256 bí mật, mã hóa bằng khóa công khai RSA của Server rồi gửi đi.
   - Server giải mã bằng khóa bí mật RSA để thu được khóa phiên AES-256. Kể từ đây, mọi dữ liệu chat được mã hóa bằng khóa phiên này.
4. **Lưu trữ lịch sử**:
   - **Server DB (MySQL)**: Lưu tin nhắn và file dưới dạng mã hóa AES (Base64) nhằm bảo vệ dữ liệu khi server bị tấn công.
   - **Client DB (SQLite)**: Lưu tin nhắn dạng văn bản thường (plaintext) cục bộ tại `%USERPROFILE%\.securechat\{username}_history.db` phục vụ tra cứu lịch sử offline.

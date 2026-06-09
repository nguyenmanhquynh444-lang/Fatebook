@echo off
REM =============================================================
REM  generate_certs.bat
REM  Tạo CA Certificate + Server Keystore + Client Truststore
REM  Chạy trong thư mục: SecureChatSystem\certs\
REM =============================================================

SET KEYTOOL=keytool
SET CA_ALIAS=securechat-ca
SET SERVER_ALIAS=securechat-server
SET CA_KEYSTORE=ca.keystore
SET SERVER_KEYSTORE=..\server\src\main\resources\server.keystore
SET CLIENT_TRUSTSTORE=..\client\src\main\resources\client.truststore
SET CA_CERT=ca.crt
SET SERVER_CSR=server.csr
SET SERVER_CERT=server.crt

SET CA_PASS=ca_password_2024
SET SERVER_PASS=server_password_2024
SET TRUST_PASS=trust_password_2024

echo ============================================================
echo  [1/7] Tao Root CA KeyPair...
echo ============================================================
%KEYTOOL% -genkeypair ^
    -alias %CA_ALIAS% ^
    -keyalg RSA -keysize 2048 ^
    -dname "CN=SecureChat Root CA, OU=VHU LTM, O=VHU, L=HCM, ST=HCM, C=VN" ^
    -keystore %CA_KEYSTORE% ^
    -storepass %CA_PASS% ^
    -keypass %CA_PASS% ^
    -validity 3650 ^
    -ext bc:c

echo.
echo ============================================================
echo  [2/7] Export CA Certificate...
echo ============================================================
%KEYTOOL% -exportcert ^
    -alias %CA_ALIAS% ^
    -keystore %CA_KEYSTORE% ^
    -storepass %CA_PASS% ^
    -file %CA_CERT% ^
    -rfc

echo.
echo ============================================================
echo  [3/7] Tao Server KeyPair...
echo ============================================================
%KEYTOOL% -genkeypair ^
    -alias %SERVER_ALIAS% ^
    -keyalg RSA -keysize 2048 ^
    -dname "CN=localhost, OU=Chat Server, O=VHU, L=HCM, ST=HCM, C=VN" ^
    -keystore %SERVER_KEYSTORE% ^
    -storepass %SERVER_PASS% ^
    -keypass %SERVER_PASS% ^
    -validity 365

echo.
echo ============================================================
echo  [4/7] Tao Certificate Signing Request (CSR)...
echo ============================================================
%KEYTOOL% -certreq ^
    -alias %SERVER_ALIAS% ^
    -keystore %SERVER_KEYSTORE% ^
    -storepass %SERVER_PASS% ^
    -file %SERVER_CSR%

echo.
echo ============================================================
echo  [5/7] CA ky Server Certificate...
echo ============================================================
%KEYTOOL% -gencert ^
    -alias %CA_ALIAS% ^
    -keystore %CA_KEYSTORE% ^
    -storepass %CA_PASS% ^
    -infile %SERVER_CSR% ^
    -outfile %SERVER_CERT% ^
    -rfc ^
    -validity 365 ^
    -ext san=ip:127.0.0.1,dns:localhost

echo.
echo ============================================================
echo  [6/7] Import CA + Server cert vao Server Keystore...
echo ============================================================
%KEYTOOL% -importcert ^
    -alias %CA_ALIAS% ^
    -keystore %SERVER_KEYSTORE% ^
    -storepass %SERVER_PASS% ^
    -file %CA_CERT% ^
    -noprompt

%KEYTOOL% -importcert ^
    -alias %SERVER_ALIAS% ^
    -keystore %SERVER_KEYSTORE% ^
    -storepass %SERVER_PASS% ^
    -file %SERVER_CERT% ^
    -noprompt

echo.
echo ============================================================
echo  [7/7] Tao Client Truststore (chi chua CA cert)...
echo ============================================================
%KEYTOOL% -importcert ^
    -alias %CA_ALIAS% ^
    -keystore %CLIENT_TRUSTSTORE% ^
    -storepass %TRUST_PASS% ^
    -file %CA_CERT% ^
    -noprompt

echo.
echo ============================================================
echo  HOAN THANH! Files da tao:
echo    - %CA_KEYSTORE%         (CA private key)
echo    - %CA_CERT%             (CA certificate)
echo    - %SERVER_KEYSTORE%     (Server keystore)
echo    - %CLIENT_TRUSTSTORE%   (Client truststore)
echo ============================================================
pause

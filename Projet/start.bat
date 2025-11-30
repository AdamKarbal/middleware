cd /d "%~dp0"

echo [1/5] Lancement du ProxyCache (Port 8090)...
start "ProxyCache" "ProxyCache\ProxyCache\bin\Debug\ProxyCache.exe"
timeout /t 2 /nobreak > nul

echo [2/5] Lancement du GPS Server (Port 8000)...
start "GPS Server" "GPSserver\GPSserver\bin\Debug\GPSserver.exe"

echo [3/5] Lancement du Notif Server (ActiveMQ)...
start "Notif Server" "NotifServer\bin\Debug\NotifServer.exe"

echo [4/5] Lancement du Web Server (Port 8080)...
start "Serveur Web" "WebServer\bin\Debug\WebServer.exe"

echo [5/5] Lancement du Client Lourd...
start "Client Lourd" "HeavyClient\bin\Debug\net8.0\HeavyClient.exe"

timeout /t 2 /nobreak > nul
start http://localhost:8080/page2.html

echo.
echo Appuyez sur une touche pour tout fermer (ou fermez les fenetres manuellement).
pause
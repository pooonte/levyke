import os
import sys
import json
import urllib.parse
from http.server import HTTPServer, BaseHTTPRequestHandler

MUSIC_FOLDER = r"C:\music"
PORT = 8080

if len(sys.argv) > 1:
    MUSIC_FOLDER = sys.argv[1]
if len(sys.argv) > 2:
    PORT = int(sys.argv[2])

class MusicHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path = urllib.parse.unquote(parsed.path)

        # Список треков
        if path == "/" or path == "/api/tracks":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()

            tracks = []
            for root, _, files in os.walk(MUSIC_FOLDER):
                for file in files:
                    if file.lower().endswith(('.mp3', '.flac', '.m4a', '.wma', '.wav')):
                        full_path = os.path.join(root, file)
                        rel_path = os.path.relpath(full_path, MUSIC_FOLDER).replace("\\", "/")
                        tracks.append({
                            "path": rel_path,
                            "name": os.path.splitext(file)[0],
                            "folder": os.path.basename(root)
                        })
            
            self.wfile.write(json.dumps(tracks).encode("utf-8"))
            return

        # Стриминг с поддержкой Range (перемотка!)
        if path.startswith("/stream/"):
            filename = path[8:]
            filepath = os.path.join(MUSIC_FOLDER, filename)

            if os.path.exists(filepath):
                file_size = os.path.getsize(filepath)
                
                # Проверяем заголовок Range
                range_header = self.headers.get('Range')
                
                if range_header:
                    # Клиент просит часть файла (перемотка)
                    range_value = range_header.replace('bytes=', '').split('-')
                    start = int(range_value[0])
                    end = int(range_value[1]) if range_value[1] else file_size - 1
                    
                    self.send_response(206)  # Partial Content
                    self.send_header("Content-Type", "audio/mpeg")
                    self.send_header("Content-Range", f"bytes {start}-{end}/{file_size}")
                    self.send_header("Content-Length", str(end - start + 1))
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.send_header("Accept-Ranges", "bytes")
                    self.end_headers()
                    
                    with open(filepath, "rb") as f:
                        f.seek(start)
                        remaining = end - start + 1
                        chunk_size = 8192
                        while remaining > 0:
                            chunk = f.read(min(chunk_size, remaining))
                            if not chunk:
                                break
                            self.wfile.write(chunk)
                            remaining -= len(chunk)
                else:
                    # Клиент хочет весь файл
                    self.send_response(200)
                    self.send_header("Content-Type", "audio/mpeg")
                    self.send_header("Content-Length", str(file_size))
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.send_header("Accept-Ranges", "bytes")
                    self.end_headers()
                    
                    with open(filepath, "rb") as f:
                        while True:
                            chunk = f.read(8192)
                            if not chunk:
                                break
                            self.wfile.write(chunk)
                
                return
            else:
                self.send_response(404)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps({"error": "File not found"}).encode("utf-8"))
                return

        self.send_response(404)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps({"error": "Not found"}).encode("utf-8"))

    def do_OPTIONS(self):
        # CORS preflight
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Range, Content-Type")
        self.end_headers()

    def log_message(self, format, *args):
        # Красивый лог вместо кракозябр
        print(f"[{self.log_date_time_string()}] {format % args}")

if __name__ == "__main__":
    print(f"🎵 Сервер запущен")
    print(f"📂 Папка: {MUSIC_FOLDER}")
    print(f"🔌 Порт: {PORT}")
    print(f"🌐 Твой IP: {os.popen('ipconfig | findstr /i "IPv4"').read().strip()}")
    print("⏎ Нажми Ctrl+C для остановки")
    
    server = HTTPServer(("0.0.0.0", PORT), MusicHandler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n🛑 Остановка сервера...")
        server.shutdown()
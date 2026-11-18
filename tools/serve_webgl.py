"""Serve a Unity WebGL build on localhost with the headers Brotli output needs.

Usage: python tools/serve_webgl.py [--dir game/Builds/WebGL] [--port 8765]
"""
import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

HOST = "127.0.0.1"

_BROTLI_TYPES = {
    ".wasm.br": "application/wasm",
    ".js.br": "application/javascript",
    ".data.br": "application/octet-stream",
}


def headers_for(path):
    """Return the Content-Type/Content-Encoding overrides for a request path."""
    clean = urlsplit(path).path
    for suffix, content_type in _BROTLI_TYPES.items():
        if clean.endswith(suffix):
            return {"Content-Type": content_type, "Content-Encoding": "br"}
    return {}


class Handler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        return headers_for(path).get("Content-Type") or super().guess_type(path)

    def end_headers(self):
        encoding = headers_for(self.path).get("Content-Encoding")
        if encoding:
            self.send_header("Content-Encoding", encoding)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dir", default="game/Builds/WebGL")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()

    handler = functools.partial(Handler, directory=args.dir)
    with ThreadingHTTPServer((HOST, args.port), handler) as server:
        print(f"Serving {args.dir} at http://{HOST}:{args.port}/ (Ctrl+C to stop)")
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()

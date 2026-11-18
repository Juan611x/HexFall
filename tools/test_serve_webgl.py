import unittest

from serve_webgl import headers_for


class HeadersForTests(unittest.TestCase):
    def test_wasm_brotli(self):
        self.assertEqual(
            headers_for("/Build/WebGL.wasm.br"),
            {"Content-Type": "application/wasm", "Content-Encoding": "br"},
        )

    def test_js_brotli(self):
        self.assertEqual(
            headers_for("/Build/WebGL.framework.js.br"),
            {"Content-Type": "application/javascript", "Content-Encoding": "br"},
        )

    def test_data_brotli(self):
        self.assertEqual(
            headers_for("/Build/WebGL.data.br"),
            {"Content-Type": "application/octet-stream", "Content-Encoding": "br"},
        )

    def test_plain_html_has_no_overrides(self):
        self.assertEqual(headers_for("/index.html"), {})

    def test_query_string_is_ignored(self):
        self.assertEqual(
            headers_for("/Build/WebGL.wasm.br?v=1")["Content-Encoding"], "br"
        )


if __name__ == "__main__":
    unittest.main()

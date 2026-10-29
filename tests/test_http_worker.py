import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

spec = importlib.util.spec_from_file_location("http_worker", Path(__file__).parents[1] / "src/Transport/http_worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class HttpWorkerTests(unittest.TestCase):
    def test_proxy_credentials_tls_and_session_configuration(self):
        with patch.object(worker.requests, "Session") as factory:
            worker.create_session({"timeout": 30, "proxy": {"server": "http://localhost:8080",
                                  "username": "test-user", "password": "test-password"}})
            settings = factory.call_args.kwargs
            self.assertEqual(("test-user", "test-password"), settings["proxy_auth"])
            self.assertEqual("http://localhost:8080", settings["proxy"])
            self.assertTrue(settings["verify"])
            self.assertFalse(settings["trust_env"])
            self.assertFalse(settings["allow_redirects"])
            self.assertEqual("safari", settings["impersonate"])

    def test_host_guard_prevents_requests_to_other_services(self):
        session = Mock()
        for url in ["http://www.futbin.com/", "https://futbin.com.evil/", "https://user@futbin.com/",
                    "https://futbin.com:8080/", "file:///etc/passwd"]:
            self.assertEqual({"error": "invalid_url"}, worker.fetch(session, url))
        session.get.assert_not_called()

    def test_one_session_reused_and_exceptions_never_disclose_credentials(self):
        session = Mock()
        session.get.side_effect = RuntimeError("test-secret-password")
        messages = [{"timeout": 5}, {"url": "https://www.futbin.com/27/players"},
                    {"url": "https://www.futbin.com/27/player/8/kylian-mbappe/market"}]
        output = io.StringIO()
        with patch.object(worker, "create_session", return_value=session) as factory, \
                patch.object(worker.sys, "stdin", io.StringIO("\n".join(map(json.dumps, messages)))), \
                patch.object(worker.sys, "stdout", output):
            worker.main()
        self.assertNotIn("test-secret", output.getvalue())
        self.assertEqual(2, session.get.call_count)
        self.assertEqual(1, factory.call_count)
        session.close.assert_called_once()


if __name__ == "__main__":
    unittest.main()

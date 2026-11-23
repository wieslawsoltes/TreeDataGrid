"""Exercise the actual published-consumer verdict without launching Chromium."""
import importlib.util
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest

spec = importlib.util.spec_from_file_location('browser_validation', Path(__file__).with_name('run-uno-browser.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class FakePage:
    def __init__(self, event=None, shutdown_failure=False):
        self.handlers = {}
        self.event = event
        self.shutdown_failure = shutdown_failure
        self.origin = None
    def on(self, event, callback): self.handlers[event] = callback
    def goto(self, url, **kwargs):
        self.origin = url.split('/?')[0]
        if self.event:
            event, suffix, value = self.event
            url = suffix if suffix.startswith('https:') else self.origin + suffix
            self.handlers[event](SimpleNamespace(url=url, failure=value, status=value))
    def wait_for_function(self, *args, **kwargs): pass
    def evaluate(self, expression):
        if expression.endswith('crossOriginIsolated'): return True
        if expression.endswith('userAgent'): return 'fake-browser'
        return {'complete': True, 'passed': True}
    def locator(self, *args): return SimpleNamespace(count=lambda: 1)
    def screenshot(self, **kwargs): pass


class FakeContext:
    def __init__(self, page):
        self.page = page
        self.tracing = SimpleNamespace(start=lambda **kwargs: None, stop=lambda **kwargs: None)
    def new_page(self): return self.page
    def close(self):
        if self.page.shutdown_failure:
            self.page.handlers['requestfailed'](SimpleNamespace(url=self.page.origin + '/pending', failure='net::ERR_ABORTED'))


class BrowserValidationTests(unittest.TestCase):
    def run_case(self, event=None, shutdown_failure=False):
        page = FakePage(event, shutdown_failure)
        browser = SimpleNamespace(new_context=lambda **kwargs: FakeContext(page))
        with TemporaryDirectory() as folder:
            root = Path(folder)
            result = module.run_sample(browser, 'probe', root, root / 'result', 10_000)
            self.assertTrue((root / 'result/result.json').exists())
            return result

    def test_success_still_requires_a_completed_application(self):
        self.assertTrue(self.run_case()['passed'])

    def test_same_origin_connection_failure_cannot_be_hidden_by_success_state(self):
        result = self.run_case(('requestfailed', '/assemblies/grid.wasm', 'net::ERR_CONNECTION_RESET'))
        self.assertFalse(result['passed'])
        self.assertTrue(any('grid.wasm' in failure for failure in result['failures']))

    def test_same_origin_abort_during_validation_is_not_silently_accepted(self):
        self.assertFalse(self.run_case(('requestfailed', '/fonts/grid.woff2', 'net::ERR_ABORTED'))['passed'])

    def test_optional_external_failure_remains_logged_not_a_local_publish_failure(self):
        self.assertTrue(self.run_case(('requestfailed', 'https://external.invalid/image', 'net::ERR_NAME_NOT_RESOLVED'))['passed'])

    def test_http_error_remains_a_failure(self):
        self.assertFalse(self.run_case(('response', '/missing.dll', 404))['passed'])

    def test_teardown_abort_does_not_retroactively_fail_completed_execution(self):
        self.assertTrue(self.run_case(shutdown_failure=True)['passed'])


if __name__ == '__main__': unittest.main()

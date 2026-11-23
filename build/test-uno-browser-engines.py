"""Engine selection and environment failures in the actual browser driver."""
import importlib.util
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import Mock

spec = importlib.util.spec_from_file_location('engine_driver', Path(__file__).with_name('run-uno-browser.py'))
driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(driver)


class Page:
    def __init__(self, canvas=1, isolated=True, result=None):
        self.canvas, self.isolated = canvas, isolated
        self.result = {'complete': True, 'passed': True} if result is None else result
    def on(self, *args): pass
    def goto(self, *args, **kwargs): pass
    def wait_for_function(self, *args, **kwargs): pass
    def evaluate(self, expression):
        if expression.endswith('crossOriginIsolated'): return self.isolated
        if expression.endswith('userAgent'): return 'test-engine'
        return self.result
    def locator(self, *args): return SimpleNamespace(count=lambda: self.canvas)
    def screenshot(self, **kwargs): pass


class BrowserEngineTests(unittest.TestCase):
    def test_each_engine_is_selected_explicitly_without_fallback(self):
        for engine in driver.ENGINES:
            with self.subTest(engine=engine):
                engines = {name: SimpleNamespace(launch=Mock(return_value=name)) for name in driver.ENGINES}
                self.assertEqual(engine, driver.launch_browser(SimpleNamespace(**engines), engine))
                engines[engine].launch.assert_called_once_with(headless=True)
                for other in set(driver.ENGINES) - {engine}: engines[other].launch.assert_not_called()

    def test_unsupported_engine_is_rejected(self):
        with self.assertRaises(ValueError): driver.launch_browser(SimpleNamespace(), 'safari')

    def test_custom_chromium_path_is_preserved(self):
        launch = Mock()
        driver.launch_browser(SimpleNamespace(chromium=SimpleNamespace(launch=launch)), 'chromium', '/browser')
        launch.assert_called_once_with(headless=True, executable_path='/browser')

    def test_custom_executable_cannot_relabel_another_engine(self):
        for engine in ('firefox', 'webkit'):
            with self.subTest(engine=engine), self.assertRaises(ValueError):
                driver.launch_browser(SimpleNamespace(), engine, '/chromium')

    def test_engine_launch_failure_is_not_replaced_with_chromium(self):
        error = RuntimeError('engine failed')
        launch = Mock(side_effect=error)
        chromium = Mock()
        with self.assertRaises(RuntimeError) as raised:
            driver.launch_browser(SimpleNamespace(webkit=SimpleNamespace(launch=launch),
                                                   chromium=SimpleNamespace(launch=chromium)), 'webkit')
        self.assertIs(error, raised.exception)
        chromium.assert_not_called()

    def run_case(self, **kwargs):
        page = Page(**kwargs)
        context = SimpleNamespace(new_page=lambda: page, close=lambda: None,
            tracing=SimpleNamespace(start=lambda **_: None, stop=lambda **_: None))
        with TemporaryDirectory() as directory:
            root = Path(directory)
            return driver.run_sample(SimpleNamespace(new_context=lambda **_: context), 'probe', root, root / 'out', 10_000)

    def test_missing_canvas_is_not_accepted_by_a_success_flag(self):
        self.assertFalse(self.run_case(canvas=0)['passed'])

    def test_missing_isolation_is_not_accepted_by_a_success_flag(self):
        self.assertFalse(self.run_case(isolated=False)['passed'])

    def test_incomplete_application_is_rejected(self):
        self.assertFalse(self.run_case(result={'complete': False, 'passed': True})['passed'])

    def test_failed_application_is_rejected(self):
        self.assertFalse(self.run_case(result={'complete': True, 'passed': False})['passed'])

    def test_completed_native_consumer_is_accepted(self):
        self.assertTrue(self.run_case()['passed'])


if __name__ == '__main__': unittest.main()

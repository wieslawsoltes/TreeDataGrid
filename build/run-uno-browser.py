#!/usr/bin/env python3
"""Execute the published, trimmed package consumers in a real browser.

Requires the pinned Playwright dependency and its Chromium installation. Static
hosting is loopback-only. No source files or generated application files change.
A successful publish, canvas appearance or success log alone is not acceptance:
the application's completed result, page errors and HTTP failures are checked.
"""
from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import subprocess
import threading
from typing import Any
from urllib.parse import urlsplit


class StaticHandler(SimpleHTTPRequestHandler):
    def end_headers(self) -> None:
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def do_GET(self) -> None:
        if urlsplit(self.path).path == '/favicon.ico':
            self.send_response(204)
            self.end_headers()
            return
        super().do_GET()

    def log_message(self, format: str, *args: Any) -> None:
        pass


def web_root(publish_directory: Path) -> Path:
    publish_directory = publish_directory.resolve()
    candidates = (publish_directory / 'wwwroot', publish_directory)
    for candidate in candidates:
        if (candidate / 'index.html').is_file():
            return candidate
    raise FileNotFoundError(f'No published index.html under {publish_directory}')


def run_sample(browser: Any, name: str, root: Path, output: Path, timeout: int) -> dict[str, Any]:
    output.mkdir(parents=True, exist_ok=True)
    messages: list[dict[str, Any]] = []
    failures: list[str] = []
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(StaticHandler, directory=str(root)))
    worker = threading.Thread(target=server.serve_forever, daemon=True)
    worker.start()
    origin = f'http://127.0.0.1:{server.server_port}'
    context = None
    page = None
    state = None
    report: dict[str, Any] = {'name': name, 'publishedRoot': str(root), 'passed': False}
    try:
        context = browser.new_context(viewport={'width': 1280, 'height': 800}, device_scale_factor=1)
        context.tracing.start(screenshots=True, snapshots=True, sources=False)
        page = context.new_page()
        page.on('console', lambda message: messages.append({'type': message.type, 'text': message.text}))

        def page_error(error: Any) -> None:
            failures.append(str(error))
            messages.append({'type': 'pageerror', 'text': str(error)})

        def response_received(response: Any) -> None:
            if response.url.startswith(origin + '/') and response.status >= 400:
                failures.append(f'HTTP {response.status}: {response.url}')

        page.on('pageerror', page_error)
        page.on('crash', lambda _: failures.append('Browser page crashed.'))
        page.on('response', response_received)
        page.on('requestfailed', lambda request: messages.append(
            {'type': 'requestfailed', 'url': request.url, 'error': request.failure}))
        page.goto(origin + '/?smoke=1&offline=1&demo=1', wait_until='domcontentloaded', timeout=timeout)
        page.wait_for_function('globalThis.__treeDataGridSmoke?.complete === true', timeout=timeout)
        state = page.evaluate('globalThis.__treeDataGridSmoke')
        if not isinstance(state, dict) or state.get('complete') is not True or state.get('passed') is not True:
            failures.append(f'Application runtime validation failed: {state}')
        report['canvasCount'] = page.locator('canvas').count()
        report['crossOriginIsolated'] = page.evaluate('globalThis.crossOriginIsolated')
        report['userAgent'] = page.evaluate('navigator.userAgent')
    except Exception as error:
        failures.append(str(error))
    finally:
        if page is not None:
            try:
                page.screenshot(path=str(output / 'final.png'), full_page=True, timeout=10_000)
            except Exception as error:
                messages.append({'type': 'screenshot-error', 'text': str(error)})
        if context is not None:
            try:
                context.tracing.stop(path=str(output / 'trace.zip'))
            except Exception as error:
                messages.append({'type': 'trace-error', 'text': str(error)})
            try:
                context.close()
            except Exception as error:
                failures.append(f'Browser context shutdown failed: {error}')
        server.shutdown()
        server.server_close()
        worker.join(timeout=5)
        report.update(passed=not failures, result=state, failures=failures)
        (output / 'console.json').write_text(json.dumps(messages, indent=2) + '\n')
        (output / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--showcase', type=Path, required=True)
    parser.add_argument('--monitor', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path('artifacts/browser-runtime'))
    parser.add_argument('--timeout', type=int, default=180, help='Per-navigation/validation timeout in seconds.')
    parser.add_argument('--executable', type=str, default=None, help='Optional installed Chromium executable.')
    args = parser.parse_args()
    if not 10 <= args.timeout <= 600:
        parser.error('--timeout must be between 10 and 600 seconds.')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    summary: dict[str, Any] = {
        'scope': 'Published trimmed package consumers, Chromium runtime and application assertions. Not native performance or physical-input/accessibility parity.',
        'completeBrowserParityProven': False,
        'passed': False,
        'results': [],
    }
    try:
        summary['revision'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        roots = {'showcase': web_root(args.showcase), 'monitor': web_root(args.monitor)}
        from playwright.sync_api import sync_playwright
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(headless=True, executable_path=args.executable)
            try:
                summary['browserVersion'] = browser.version
                for name, root in roots.items():
                    result = run_sample(browser, name, root, output / name, args.timeout * 1000)
                    summary['results'].append(result)
                    print('UNO_BROWSER_SAMPLE=' + json.dumps(result), flush=True)
            finally:
                browser.close()
        summary['passed'] = len(summary['results']) == 2 and all(result['passed'] for result in summary['results'])
    except Exception as error:
        summary['driverError'] = str(error)
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('UNO_BROWSER_SUMMARY=' + json.dumps(summary), flush=True)
    return 0 if summary['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())

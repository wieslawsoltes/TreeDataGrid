"""External browser input protocol. Never call .NET grid methods or mutate its model."""
from __future__ import annotations
import json
import math
import time
from typing import Any

STAGES = (
    'select-row', 'arrow-down', 'begin-edit', 'commit-edit',
    'select-cancel-row', 'begin-cancel-edit', 'cancel-edit',
    'ctrl-select', 'resize-column', 'cancel-resize', 'sort-column',
    'sort-column-descending', 'sort-column-clear', 'sort-column-restart', 'wheel-scroll',
)
MARKER = 'UNO_BROWSER_INPUT_STEP='


def drive(page: Any, messages: list[dict[str, Any]], timeout: int) -> list[str]:
    completed: list[str] = []
    cursor = 0
    for expected in STAGES:
        deadline = time.monotonic() + timeout / 1000
        step = None
        while step is None:
            while cursor < len(messages):
                text = str(messages[cursor].get('text', ''))
                cursor += 1
                if MARKER not in text:
                    continue
                candidate = json.loads(text.split(MARKER, 1)[1])
                if candidate.get('name') != expected:
                    raise RuntimeError(f'Input protocol out of order: expected {expected}, received {candidate}')
                step = candidate
                break
            if step is not None:
                break
            result = page.evaluate('globalThis.__treeDataGridSmoke ?? null')
            if isinstance(result, dict) and result.get('complete'):
                raise RuntimeError(f'Application ended before input stage {expected}: {result}')
            if time.monotonic() >= deadline:
                raise TimeoutError('No application readiness signal for browser input: ' + expected)
            page.wait_for_timeout(25)

        if expected in ('arrow-down', 'begin-edit', 'begin-cancel-edit'):
            page.keyboard.press('ArrowDown' if expected == 'arrow-down' else 'F2')
        elif expected in ('commit-edit', 'cancel-edit'):
            value = step.get('text')
            if not isinstance(value, str) or len(value) > 1024:
                raise ValueError('Invalid fixture editor text.')
            page.keyboard.press('Control+A')
            page.keyboard.type(value)
            page.keyboard.press('Enter' if expected == 'commit-edit' else 'Escape')
        else:
            x, y = float(step['x']), float(step['y'])
            viewport = page.viewport_size
            if not (math.isfinite(x) and math.isfinite(y) and viewport and
                    0 <= x < viewport['width'] and 0 <= y < viewport['height']):
                raise ValueError(f'Input target is outside the browser viewport: {step}')
            if expected == 'ctrl-select':
                page.keyboard.down('Control')
                try:
                    page.mouse.click(x, y)
                finally:
                    page.keyboard.up('Control')
            elif expected in ('resize-column', 'cancel-resize'):
                page.mouse.move(x, y)
                page.mouse.down()
                try:
                    page.mouse.move(x + 40, y, steps=8)
                finally:
                    page.mouse.up()
            elif expected == 'wheel-scroll':
                page.mouse.move(x, y)
                page.mouse.wheel(0, 500)
            else:
                page.mouse.click(x, y)
        completed.append(expected)
    return completed

"""External browser input protocol. Never call .NET grid methods or mutate its model."""
from __future__ import annotations
import json
import math
import time
from typing import Any

STAGES = (
    'select-row', 'arrow-down', 'begin-edit', 'type-edit', 'commit-edit',
    'select-cancel-row', 'begin-cancel-edit', 'type-cancel-edit', 'cancel-edit',
    'ctrl-select', 'resize-column', 'cancel-resize', 'sort-column',
    'sort-column-descending', 'sort-column-clear', 'sort-column-restart', 'wheel-scroll',
)
MARKER = 'UNO_BROWSER_INPUT_STEP='


_EDITOR_STATE = '''(install) => {
    const extension = globalThis.Uno?.UI?.Runtime?.Skia?.BrowserInvisibleTextBoxViewExtension;
    if (install && !globalThis.__tdgKeyProbe) {
        globalThis.__tdgKeyProbe = [];
        for (const type of ['keydown', 'keyup', 'compositionstart', 'compositionend']) {
            document.addEventListener(type, ev => globalThis.__tdgKeyProbe.push(
                { type, key: ev.key ?? null, isComposing: ev.isComposing ?? null }), true);
        }
    }
    const active = document.activeElement;
    return {
        composing: extension ? !!extension.isComposing : null,
        active: active ? active.tagName + (extension && active === extension.inputElement ? ':invisible-input' : '') : null,
        events: (globalThis.__tdgKeyProbe ?? []).splice(0),
    };
}'''


def _editor_state(page: Any, install_probe: bool = False) -> dict[str, Any]:
    """Diagnostic browser-side editor state; never fails the input protocol."""
    evaluate = getattr(page, 'evaluate', None)
    if evaluate is None:
        return {}
    try:
        state = evaluate(_EDITOR_STATE, install_probe)
        return state if isinstance(state, dict) else {}
    except Exception as error:  # diagnostics only
        return {'error': str(error)[:200]}


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
        elif expected in ('type-edit', 'type-cancel-edit'):
            value = step.get('text')
            if not isinstance(value, str) or len(value) > 1024:
                raise ValueError('Invalid fixture editor text.')
            page.keyboard.press('Control+A')
            # Committed text, not invented virtual keys for non-ASCII scalars.
            # This covers Unicode writeback; it is not an OS IME simulation.
            page.keyboard.insert_text(value)
        elif expected in ('commit-edit', 'cancel-edit'):
            # The application requests the key only after its editor holds the inserted
            # text, so the committing or cancelling key cannot race the text input. A key
            # pressed while the browser still reports a text composition belongs to the
            # composition, so wait for the browser to finish it first.
            key = 'Enter' if expected == 'commit-edit' else 'Escape'
            before = _editor_state(page, install_probe=True)
            composition_deadline = time.monotonic() + 5
            while before.get('composing') and time.monotonic() < composition_deadline:
                time.sleep(0.05)
                before = _editor_state(page)
            page.keyboard.press(key)
            print('UNO_BROWSER_INPUT_KEY_STATE=' + json.dumps(
                {'stage': expected, 'key': key, 'before': before, 'after': _editor_state(page)}), flush=True)
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

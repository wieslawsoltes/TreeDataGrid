"""Exercise the real external input protocol without substituting .NET calls."""
import json
import unittest
from types import SimpleNamespace
from uno_browser_input import MARKER, STAGES, drive


class Page:
    viewport_size = {'width': 1280, 'height': 800}

    def __init__(self, fail=None):
        self.events = []
        def record(name):
            def operation(*args, **kwargs):
                self.events.append((name, args, kwargs))
                if name == fail:
                    raise RuntimeError('injected input failure')
            return operation
        self.keyboard = SimpleNamespace(press=record('press'), insert_text=record('insert'),
                                        down=record('key-down'), up=record('key-up'))
        self.mouse = SimpleNamespace(click=record('click'), move=record('move'),
                                     down=record('mouse-down'), up=record('mouse-up'), wheel=record('wheel'))


def messages(text='Zażółć gęślą jaźń 日本語 🌲 👩‍💻'):
    return [{'text': MARKER + json.dumps({'name': stage, 'x': 50, 'y': 50, 'text': text})}
            for stage in STAGES]


class BrowserInputTests(unittest.TestCase):
    def test_committed_unicode_is_inserted_exactly_without_virtual_key_invention(self):
        page = Page()
        text = 'Zażółć gęślą jaźń 日本語 🌲 👩‍💻'
        self.assertEqual(list(STAGES), drive(page, messages(text), 10_000))
        inserted = [event for event in page.events if event[0] == 'insert']
        self.assertEqual([('insert', (text,), {}), ('insert', (text,), {})], inserted)
        for index, event in enumerate(page.events):
            if event[0] == 'insert':
                self.assertEqual(('press', ('Control+A',), {}), page.events[index - 1])
                self.assertIn(page.events[index + 1], [('press', ('Enter',), {}), ('press', ('Escape',), {})])

    def test_missing_or_oversized_text_cannot_be_sent_to_an_editor(self):
        for text in (None, 123, {}, 'x' * 1025):
            with self.subTest(text_type=type(text).__name__), self.assertRaises(ValueError):
                drive(Page(), messages(text), 10_000)

    def test_out_of_order_protocol_is_rejected_before_any_input(self):
        data = messages()
        data[0], data[1] = data[1], data[0]
        page = Page()
        with self.assertRaises(RuntimeError): drive(page, data, 10_000)
        self.assertEqual([], page.events)

    def test_pointer_coordinates_are_finite_and_in_the_browser_viewport(self):
        for x in (-1, 1280, float('inf'), float('nan')):
            with self.subTest(x=x):
                data = messages()
                data[0] = {'text': MARKER + json.dumps({'name': STAGES[0], 'x': x, 'y': 10})}
                page = Page()
                with self.assertRaises(ValueError): drive(page, data, 10_000)
                self.assertEqual([], page.events)

    def test_failed_drag_still_releases_the_pressed_pointer(self):
        page = Page(fail='move')
        # Fail the second move, after the mouse button is down.
        original = page.mouse.move
        calls = 0
        def move(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 1: page.events.append(('move', args, kwargs))
            else: original(*args, **kwargs)
        page.mouse.move = move
        with self.assertRaises(RuntimeError): drive(page, messages(), 10_000)
        self.assertEqual('mouse-up', page.events[-1][0])
        self.assertIn('mouse-down', [event[0] for event in page.events])

    def test_failed_control_click_still_releases_the_modifier(self):
        page = Page()
        original = page.mouse.click
        def click(*args, **kwargs):
            if page.events and page.events[-1][0] == 'key-down':
                raise RuntimeError('injected control-click failure')
            original(*args, **kwargs)
        page.mouse.click = click
        with self.assertRaises(RuntimeError): drive(page, messages(), 10_000)
        self.assertEqual(('key-up', ('Control',), {}), page.events[-1])


if __name__ == '__main__': unittest.main()

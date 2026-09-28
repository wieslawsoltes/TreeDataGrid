#!/usr/bin/env python3
"""Verify exact runtime flattening and the reference's explicit state-root correction."""
from copy import deepcopy
from pathlib import Path
import hashlib
import subprocess
import xml.etree.ElementTree as ET

BASELINE = '79cf4279c88a69dcd4af90621066a250fe84634f'
PATH = 'src/TreeDataGrid.Controls.Uno/Themes/Generic.xaml'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'


def verify_reference(before: bytes, reference: ET.Element) -> None:
    baseline = ET.fromstring(before)
    original = next(element for element in baseline.iter(XAML + 'ControlTemplate')
                    if element.attrib.get('TargetType') == 'primitives:TreeDataGridTextCell')
    expected = deepcopy(original)
    root = expected[0]
    inner = root.find(XAML + 'Grid')
    if inner is None: raise ValueError('Expected the original nested Grid.')
    groups = inner.find(XAML + 'VisualStateManager.VisualStateGroups')
    if groups is None: raise ValueError('Expected the original nested state groups.')
    # GoToState only inspects the template root. This explicitly activates the
    # intended reference states without changing layout, visual order or setters.
    inner.remove(groups)
    root.insert(0, groups)
    templates = [element for element in reference.iter(XAML + 'ControlTemplate')
                 if element.attrib.get(X + 'Key') == 'ReferenceTextTemplate']
    if len(templates) != 1: raise ValueError('Expected one reference template.')
    actual = templates[0]
    def shape(element, template):
        attributes = dict(element.attrib)
        if element is template:
            attributes.pop(X + 'Key', None)
            attributes['TargetType'] = attributes['TargetType'].split(':')[-1]
        return (element.tag, attributes, (element.text or '').strip(),
                tuple(shape(child, template) for child in element))
    if shape(expected, expected) != shape(actual, actual):
        raise ValueError('Reference changed beyond the explicit state-group root relocation.')


def verify(root: Path) -> dict:
    before = subprocess.check_output(['git', '-C', str(root), 'show', BASELINE + ':' + PATH])
    expected_hash = hashlib.sha1(b'blob ' + str(len(before)).encode() + b'\0' + before).hexdigest()
    if expected_hash != '46ae157983d41442d27faf44580ab8ce050c43d8':
        raise ValueError('Unexpected original theme input.')
    text = before.decode('utf-8')
    start = text.index('<ControlTemplate TargetType="primitives:TreeDataGridTextCell">')
    end = text.index('</ControlTemplate>', start) + len('</ControlTemplate>')
    block = text[start:end]
    for old, new in (
        ('<Border x:Name="CellBorder" DataContext="{x:Null}"', '<Grid x:Name="CellBorder" DataContext="{x:Null}"'),
        ('            <Grid>\n', ''),
        ('            </Grid>\n          </Border>', '          </Grid>'),
    ):
        if block.count(old) != 1: raise ValueError('Nonunique reviewed template transformation.')
        block = block.replace(old, new, 1)
    expected = (text[:start] + block + text[end:]).encode().removesuffix(b'\n')
    actual = (root / PATH).read_bytes()
    if actual != expected: raise ValueError('Candidate theme differs from the exact reviewed substitutions and terminal-LF omission.')
    changed = subprocess.check_output(['git', '-C', str(root), 'diff', '--name-only', BASELINE, 'HEAD', '--', 'src'], text=True).splitlines()
    if changed != [PATH]: raise ValueError('This experiment must not change another runtime source: ' + repr(changed))
    verify_reference(before, ET.parse(root / 'samples/TreeDataGridUnoSample/TextTemplateParityView.xaml').getroot())
    return {'baseline': BASELINE, 'originalThemeBlob': expected_hash,
            'candidateThemeSha256': hashlib.sha256(actual).hexdigest(),
            'terminalLfOmitted': True, 'runtimePaths': changed,
            'referenceLayoutMatches': True, 'referenceStateGroupsRelocatedToRoot': True,
            'historicalInactiveStatesAreNotVisualReference': True}


if __name__ == '__main__':
    import json
    print('UNO_TEXT_TEMPLATE_INPUT=' + json.dumps(verify(Path(__file__).resolve().parent.parent)), flush=True)

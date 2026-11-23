#!/usr/bin/env python3
"""Verify equivalent active-state baselines and exact native template flattening."""
from copy import deepcopy
from pathlib import Path
import hashlib
import subprocess
import xml.etree.ElementTree as ET

# Historical source remains the frozen visual/reference input, never a silently
# revised baseline. The measured control fixes its inactive state attachment.
BASELINE = '79cf4279c88a69dcd4af90621066a250fe84634f'
ACTIVE_BASELINE = 'f1fb840ac12aca8851fd5d55589b80ece72bf6fc'
PATH = 'src/TreeDataGrid.Controls.Uno/Themes/Generic.xaml'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'


def template_bounds(text: str) -> tuple[int, int]:
    start = text.index('<ControlTemplate TargetType="primitives:TreeDataGridTextCell">')
    end = text.index('</ControlTemplate>', start) + len('</ControlTemplate>')
    return start, end


def replace_once(text: str, before: str, after: str) -> str:
    if text.count(before) != 1: raise ValueError('Nonunique reviewed template transformation.')
    return text.replace(before, after, 1)


def verify_control(historical: bytes, control: bytes) -> None:
    text = historical.decode('utf-8')
    start, end = template_bounds(text)
    block = replace_once(text[start:end], '            <Grid>\n', '')
    block = replace_once(block, '              </VisualStateManager.VisualStateGroups>\n',
                         '              </VisualStateManager.VisualStateGroups>\n            <Grid>\n')
    if control != (text[:start] + block + text[end:]).encode('utf-8'):
        raise ValueError('The measured nested control changed beyond moving its state groups to the root.')


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


def verify(root: Path, measured_baseline: str = ACTIVE_BASELINE) -> dict:
    if measured_baseline != ACTIVE_BASELINE:
        raise ValueError('The native comparison must use the exact active-state nested control.')
    before = subprocess.check_output(['git', '-C', str(root), 'show', BASELINE + ':' + PATH])
    historical_blob = hashlib.sha1(b'blob ' + str(len(before)).encode() + b'\0' + before).hexdigest()
    if historical_blob != '46ae157983d41442d27faf44580ab8ce050c43d8':
        raise ValueError('Unexpected original theme input.')
    control = subprocess.check_output(['git', '-C', str(root), 'show', measured_baseline + ':' + PATH])
    verify_control(before, control)
    if subprocess.check_output(['git', '-C', str(root), 'diff', '--name-only', BASELINE, measured_baseline, '--', 'src'],
                               text=True).splitlines() != [PATH]:
        raise ValueError('The active-state control modified another runtime source.')
    text = before.decode('utf-8')
    start, end = template_bounds(text)
    block = text[start:end]
    for old, new in (
        ('<Border x:Name="CellBorder" DataContext="{x:Null}"', '<Grid x:Name="CellBorder" DataContext="{x:Null}"'),
        ('            <Grid>\n', ''),
        ('            </Grid>\n          </Border>', '          </Grid>'),
    ):
        block = replace_once(block, old, new)
    expected = (text[:start] + block + text[end:]).encode().removesuffix(b'\n')
    actual = (root / PATH).read_bytes()
    if actual != expected: raise ValueError('Candidate theme differs from exact flattening and terminal-LF omission.')
    changed = subprocess.check_output(['git', '-C', str(root), 'diff', '--name-only', measured_baseline, 'HEAD', '--', 'src'],
                                      text=True).splitlines()
    if changed != [PATH]: raise ValueError('The measured experiment changed another runtime source: ' + repr(changed))
    verify_reference(before, ET.parse(root / 'samples/TreeDataGridUnoSample/TextTemplateParityView.xaml').getroot())
    return {'historicalInput': BASELINE, 'baseline': measured_baseline, 'originalThemeBlob': historical_blob,
            'activeNestedThemeSha256': hashlib.sha256(control).hexdigest(),
            'candidateThemeSha256': hashlib.sha256(actual).hexdigest(),
            'terminalLfOmitted': True, 'runtimePaths': changed,
            'bothMeasuredTemplatesHaveRootStates': True,
            'referenceLayoutMatches': True, 'referenceStateGroupsRelocatedToRoot': True,
            'historicalInactiveStatesAreNotMeasuredBaseline': True}


if __name__ == '__main__':
    import argparse
    import json
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', default=ACTIVE_BASELINE)
    args = parser.parse_args()
    print('UNO_TEXT_TEMPLATE_INPUT=' + json.dumps(verify(Path(__file__).resolve().parent.parent, args.baseline)), flush=True)

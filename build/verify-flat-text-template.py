#!/usr/bin/env python3
"""Verify that the native template experiment changes only its reviewed visual layer."""
from pathlib import Path
import hashlib
import subprocess
import xml.etree.ElementTree as ET

BASELINE = '79cf4279c88a69dcd4af90621066a250fe84634f'
PATH = 'src/TreeDataGrid.Controls.Uno/Themes/Generic.xaml'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'


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
    expected = (text[:start] + block + text[end:]).encode()
    actual = (root / PATH).read_bytes()
    # XML artifacts in the preceding head have no final newline. Do not silently
    # normalize the rest of the file or reserialize unrelated templates.
    if actual != expected: raise ValueError('Candidate theme differs from the exact three reviewed substitutions.')
    changed = subprocess.check_output(['git', '-C', str(root), 'diff', '--name-only', BASELINE, 'HEAD', '--', 'src'], text=True).splitlines()
    if changed != [PATH]: raise ValueError('This experiment must not change another runtime source: ' + repr(changed))

    baseline = ET.fromstring(before)
    reference = ET.parse(root / 'samples/TreeDataGridUnoSample/TextTemplateParityView.xaml').getroot()
    original_template = next(element for element in baseline.iter(XAML + 'ControlTemplate')
                             if element.attrib.get('TargetType') == 'primitives:TreeDataGridTextCell')
    reference_template = next(element for element in reference.iter(XAML + 'ControlTemplate')
                              if element.attrib.get(X + 'Key') == 'ReferenceTextTemplate')
    def shape(element):
        attributes = dict(element.attrib)
        if element is original_template or element is reference_template:
            attributes.pop(X + 'Key', None)
            attributes['TargetType'] = attributes['TargetType'].split(':')[-1]
        return (element.tag, attributes, (element.text or '').strip(), tuple(shape(child) for child in element))
    if shape(original_template) != shape(reference_template):
        raise ValueError('The compiled visual reference does not preserve the original template structure.')
    return {'baseline': BASELINE, 'originalThemeBlob': expected_hash,
            'candidateThemeSha256': hashlib.sha256(actual).hexdigest(),
            'runtimePaths': changed, 'referenceTemplateMatches': True}


if __name__ == '__main__':
    import json
    print('UNO_TEXT_TEMPLATE_INPUT=' + json.dumps(verify(Path(__file__).resolve().parent.parent)), flush=True)

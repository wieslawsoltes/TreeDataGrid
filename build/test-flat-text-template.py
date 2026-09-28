"""The visual reference may only move existing state groups, not change rendering."""
from copy import deepcopy
import importlib.util
from pathlib import Path
import subprocess
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('flat_verifier', ROOT / 'build/verify-flat-text-template.py')
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class ReferenceTemplateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.before = subprocess.check_output(['git', '-C', str(ROOT), 'show', verifier.BASELINE + ':' + verifier.PATH])
        cls.reference = ET.parse(ROOT / 'samples/TreeDataGridUnoSample/TextTemplateParityView.xaml').getroot()

    def template(self, document):
        return next(element for element in document.iter(verifier.XAML + 'ControlTemplate')
                    if element.attrib.get(verifier.X + 'Key') == 'ReferenceTextTemplate')

    def test_exact_reviewed_reference_is_accepted(self):
        verifier.verify_reference(self.before, self.reference)

    def test_historical_inactive_state_location_is_not_accepted(self):
        document = deepcopy(self.reference)
        root = self.template(document)[0]
        groups = root.find(verifier.XAML + 'VisualStateManager.VisualStateGroups')
        root.remove(groups)
        root.find(verifier.XAML + 'Grid').insert(0, groups)
        with self.assertRaises(ValueError): verifier.verify_reference(self.before, document)

    def test_visual_property_change_is_rejected(self):
        document = deepcopy(self.reference)
        self.template(document)[0].set('BorderThickness', '0')
        with self.assertRaises(ValueError): verifier.verify_reference(self.before, document)

    def test_state_setter_change_is_rejected(self):
        document = deepcopy(self.reference)
        next(self.template(document).iter(verifier.XAML + 'Setter')).set('Value', '0')
        with self.assertRaises(ValueError): verifier.verify_reference(self.before, document)

    def test_overlay_removal_is_rejected(self):
        document = deepcopy(self.reference)
        grid = self.template(document)[0].find(verifier.XAML + 'Grid')
        grid.remove(grid[-1])
        with self.assertRaises(ValueError): verifier.verify_reference(self.before, document)

    def test_overlay_reordering_is_rejected(self):
        document = deepcopy(self.reference)
        grid = self.template(document)[0].find(verifier.XAML + 'Grid')
        last = grid[-1]
        grid.remove(last)
        grid.insert(0, last)
        with self.assertRaises(ValueError): verifier.verify_reference(self.before, document)


if __name__ == '__main__': unittest.main()

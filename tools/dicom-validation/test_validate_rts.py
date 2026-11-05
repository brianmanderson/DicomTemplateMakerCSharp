"""Unit tests for validate_rts.py (standard library only).

Run from the repository root:
    python -I -m unittest discover -s tools/dicom-validation -p "test_*.py"
"""

from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path

import validate_rts


class FindToolTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def fake_file(self, path: Path) -> str:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("", encoding="utf-8")
        return str(path)

    def test_found_beside_the_venv_interpreter(self) -> None:
        # A venv puts python.exe and the console scripts pip installs in the same folder.
        scripts = self.root / "venv" / validate_rts.SCRIPTS_SUBDIR
        python = self.fake_file(scripts / "python.exe")
        tool = self.fake_file(scripts / "dciodvfy.exe")
        self.assertEqual(validate_rts.find_tool("dciodvfy", executable=python, which=lambda name: "elsewhere"), tool)

    def test_found_in_the_scripts_folder_of_a_base_interpreter(self) -> None:
        # A base install puts the console scripts in Scripts (Windows) or bin beside python.
        python = self.fake_file(self.root / "Python312" / "python.exe")
        tool = self.fake_file(self.root / "Python312" / validate_rts.SCRIPTS_SUBDIR / "validate_iods")
        self.assertEqual(validate_rts.find_tool("validate_iods", executable=python, which=lambda name: None), tool)

    def test_falls_back_to_path_when_not_beside_the_interpreter(self) -> None:
        python = self.fake_file(self.root / "venv" / "python.exe")
        asked = []

        def which(name: str) -> str:
            asked.append(name)
            return os.path.join("somewhere", "on", "path", name)

        self.assertEqual(validate_rts.find_tool("dciodvfy", executable=python, which=which),
                         os.path.join("somewhere", "on", "path", "dciodvfy"))
        self.assertEqual(asked, ["dciodvfy"])

    def test_none_when_missing_and_the_problem_names_every_location_tried(self) -> None:
        python = self.fake_file(self.root / "venv" / "python.exe")
        self.assertIsNone(validate_rts.find_tool("dciodvfy", executable=python, which=lambda name: None))

        problem = validate_rts.missing_tool_problem("dciodvfy", "dicom3tools", executable=python)
        self.assertIn("dciodvfy is not installed (pip install dicom3tools)", problem)
        for folder in (self.root / "venv", self.root / "venv" / validate_rts.SCRIPTS_SUBDIR):
            self.assertIn(str(folder / "dciodvfy"), problem)
            self.assertIn(str(folder / "dciodvfy.exe"), problem)
        self.assertIn("PATH", problem)


if __name__ == "__main__":
    unittest.main()

#!/usr/bin/env python3
import importlib.util
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("drift_check.py")
SPEC = importlib.util.spec_from_file_location("drift_check", MODULE_PATH)
drift_check = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(drift_check)


class DriftCheckTests(unittest.TestCase):
    def test_parse_current_rule_catalog(self):
        rules_md = Path(__file__).resolve().parents[2] / "spec" / "RULES.md"

        rule_ids = drift_check.parse_rule_ids(rules_md)

        self.assertEqual(
            rule_ids,
            [
                "AES-MCP-001",
                "AES-MCP-002",
                "AES-AUTH-001",
                "AES-AUTH-002",
                "AES-AUTH-004",
                "AES-AUTH-005",
                "AES-MCP-009",
                "AES-MCP-011",
                "AES-MCP-012",
                "AES-MCP-003",
                "AES-MCP-004",
                "AES-MCP-005",
                "AES-EXT-001",
                "AES-CFG-001",
                "AES-AUTH-006",
                "AES-MCP-008",
                "AES-MCP-010",
                "AES-MCP-006",
                "AES-MCP-007",
                "AES-AUTH-003",
                "AES-CFG-002",
                "AES-MCP-013",
                "AES-CFG-003",
                "AES-CFG-004",
            ],
        )

    def test_generic_deprecation_text_does_not_hide_rule_table(self):
        with tempfile.TemporaryDirectory() as tmp:
            rules_md = Path(tmp) / "RULES.md"
            rules_md.write_text(
                "\n".join(
                    [
                        "# Rules",
                        "Deprecated rules keep their ID and get `status: deprecated`.",
                        "",
                        "## Rule table",
                        "| Severity | ID | Name | Applies to |",
                        "|----------|----|------|------------|",
                        "| Critical | AES-MCP-001 | Active rule | app |",
                        "",
                        "## Rule details",
                        "### AES-MCP-001 - Active rule",
                        "",
                    ]
                ),
                encoding="utf-8",
            )

            self.assertEqual(drift_check.parse_rule_ids(rules_md), ["AES-MCP-001"])

    def test_deprecated_rule_sections_are_excluded(self):
        with tempfile.TemporaryDirectory() as tmp:
            rules_md = Path(tmp) / "RULES.md"
            rules_md.write_text(
                "\n".join(
                    [
                        "# Rules",
                        "",
                        "## Rule table",
                        "| Severity | ID | Name | Applies to |",
                        "|----------|----|------|------------|",
                        "| Critical | AES-MCP-001 | Active rule | app |",
                        "| Low | AES-CFG-999 | Old rule | app |",
                        "",
                        "## Rule details",
                        "### AES-MCP-001 - Active rule",
                        "",
                        "### AES-CFG-999 - Old rule",
                        "status: deprecated",
                        "",
                    ]
                ),
                encoding="utf-8",
            )

            self.assertEqual(drift_check.parse_rule_ids(rules_md), ["AES-MCP-001"])

    def test_check_implementations_reports_missing_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            swift_dir = root / "swift"
            csharp_dir = root / "csharp"
            swift_dir.mkdir()
            csharp_dir.mkdir()
            (swift_dir / "AESMCP001.swift").write_text("", encoding="utf-8")

            errors = drift_check.check_implementations(
                ["AES-MCP-001"],
                swift_dir,
                csharp_dir,
            )

            self.assertEqual(len(errors), 1)
            self.assertIn("MISSING C#", errors[0])
            self.assertIn("AES-MCP-001", errors[0])


if __name__ == "__main__":
    unittest.main()

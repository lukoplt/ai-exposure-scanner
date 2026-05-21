#!/usr/bin/env python3
"""
Drift check: verifies every rule ID in spec/RULES.md exists as an implementation
file in both macos/Scanner/Sources/Scanner/Rules/ and windows/Scanner/Rules/.

Expected file naming:
  Swift:  AESMCP001.swift  (rule ID AES-MCP-001 → strip dashes → AESMCP001)
  C#:     AESMCP001.cs

Exits 0 if all rules are implemented in both platforms.
Exits 1 if any rule is missing from either platform.
"""
import re
import sys
import argparse
from pathlib import Path


RULE_HEADER_RE = re.compile(r"^###\s+(AES-[A-Z]+-\d{3})\b")
RULE_TABLE_ROW_RE = re.compile(
    r"^\|\s*(?:Critical|High|Medium|Low)\s*\|\s*(AES-[A-Z]+-\d{3})\s*\|",
    re.IGNORECASE,
)


def parse_rule_ids(rules_md: Path) -> list[str]:
    """Extract all non-deprecated AES-* rule IDs from RULES.md."""
    ids = []
    deprecated_ids = set()
    current_rule_id = None

    with rules_md.open(encoding="utf-8") as f:
        for line in f:
            lower_line = line.lower()

            header_match = RULE_HEADER_RE.match(line)
            if header_match:
                current_rule_id = header_match.group(1)
                if current_rule_id not in ids:
                    ids.append(current_rule_id)
                continue

            table_match = RULE_TABLE_ROW_RE.match(line)
            if table_match:
                rule_id = table_match.group(1)
                if rule_id not in ids:
                    ids.append(rule_id)
                if "deprecated" in lower_line:
                    deprecated_ids.add(rule_id)
                continue

            if current_rule_id and "status: deprecated" in lower_line:
                deprecated_ids.add(current_rule_id)

    return [rule_id for rule_id in ids if rule_id not in deprecated_ids]


def rule_id_to_filename(rule_id: str) -> str:
    """Convert AES-MCP-001 to AESMCP001."""
    return rule_id.replace("-", "")


def check_implementations(rule_ids: list[str], swift_dir: Path, csharp_dir: Path) -> list[str]:
    """Return list of error strings for any missing implementations."""
    errors = []
    for rule_id in rule_ids:
        filename_stem = rule_id_to_filename(rule_id)

        swift_file = swift_dir / f"{filename_stem}.swift"
        if not swift_file.exists():
            errors.append(f"MISSING Swift: {swift_file} (rule {rule_id})")

        csharp_file = csharp_dir / f"{filename_stem}.cs"
        if not csharp_file.exists():
            errors.append(f"MISSING C#:    {csharp_file} (rule {rule_id})")

    return errors


def main():
    parser = argparse.ArgumentParser(description="Verify rule implementations match RULES.md")
    parser.add_argument("--rules", type=Path, default=Path("spec/RULES.md"))
    parser.add_argument("--swift", type=Path, default=Path("macos/Scanner/Sources/Scanner/Rules"))
    parser.add_argument("--csharp", type=Path, default=Path("windows/Scanner/Rules"))
    args = parser.parse_args()

    if not args.rules.exists():
        print(f"ERROR: RULES.md not found at {args.rules}", file=sys.stderr)
        sys.exit(1)

    rule_ids = parse_rule_ids(args.rules)
    if not rule_ids:
        print("WARNING: No rule IDs found in RULES.md — nothing to check.")
        sys.exit(0)

    print(f"Found {len(rule_ids)} rules in {args.rules}: {', '.join(rule_ids)}")

    # Directories may not exist yet during scaffolding — warn but don't fail
    if not args.swift.exists():
        print(f"WARNING: Swift rules dir not found: {args.swift} — skipping Swift check")
        swift_exists = False
    else:
        swift_exists = True

    if not args.csharp.exists():
        print(f"WARNING: C# rules dir not found: {args.csharp} — skipping C# check")
        csharp_exists = False
    else:
        csharp_exists = True

    errors = []
    if swift_exists or csharp_exists:
        errors = check_implementations(
            rule_ids,
            args.swift if swift_exists else Path("/dev/null"),
            args.csharp if csharp_exists else Path("/dev/null"),
        )
        # Filter errors for dirs that don't exist
        if not swift_exists:
            errors = [e for e in errors if "Swift" not in e]
        if not csharp_exists:
            errors = [e for e in errors if "C#" not in e]

    checked_platforms = []
    if swift_exists:
        checked_platforms.append("Swift")
    if csharp_exists:
        checked_platforms.append("C#")

    if errors:
        print("\nDrift check FAILED:")
        for err in errors:
            print(f"  {err}")
        print(
            f"\n{len(errors)} missing implementation(s). "
            "See CONTRIBUTING.md for how to add a new rule."
        )
        sys.exit(1)
    elif not checked_platforms:
        print("Drift check SKIPPED implementation checks — no implementation directories found.")
        sys.exit(0)
    else:
        print(
            "Drift check PASSED — all rules implemented in "
            f"{' and '.join(checked_platforms)}."
        )
        sys.exit(0)


if __name__ == "__main__":
    main()

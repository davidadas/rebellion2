#!/usr/bin/env python3
"""Compare serialized game saves while renaming generated IDs consistently.

Only GUID-shaped values are normalized. Authored IDs such as FNEMP1 and COMMENOR
remain significant. A one-to-one ID mapping preserves references and detects
cases where two nodes that shared an ID in one save do not share it in the other.
"""

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter


GENERATED_ID = re.compile(
    r"(?:[0-9a-f]{32}|[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})\Z",
    re.IGNORECASE,
)


class SaveDiff:
    def __init__(self, max_diffs):
        self.max_diffs = max_diffs
        self.difference_count = 0
        self.differences = []
        self.left_to_right = {}
        self.right_to_left = {}
        self.id_references = 0

    def report(self, path, left, right):
        self.difference_count += 1
        if len(self.differences) < self.max_diffs:
            self.differences.append((path, left, right))

    def compare_value(self, path, left, right):
        left = (left or "").strip()
        right = (right or "").strip()
        if GENERATED_ID.fullmatch(left) and GENERATED_ID.fullmatch(right):
            left, right = left.lower(), right.lower()
            self.id_references += 1
            mapped_right = self.left_to_right.get(left)
            mapped_left = self.right_to_left.get(right)
            if mapped_right is not None and mapped_right != right:
                self.report(path, f"ID previously mapped to {mapped_right}", right)
            elif mapped_left is not None and mapped_left != left:
                self.report(path, left, f"ID previously mapped from {mapped_left}")
            else:
                self.left_to_right[left] = right
                self.right_to_left[right] = left
        elif left != right:
            self.report(path, left, right)

    def compare(self, left, right, path):
        if left.tag != right.tag:
            self.report(path, left.tag, right.tag)
            return

        for name in sorted(left.attrib.keys() | right.attrib.keys()):
            if name not in left.attrib or name not in right.attrib:
                self.report(path + "/@" + name, left.attrib.get(name), right.attrib.get(name))
            else:
                self.compare_value(
                    path + "/@" + name, left.attrib[name], right.attrib[name]
                )
        self.compare_value(path, left.text, right.text)

        left_children = list(left)
        right_children = list(right)
        if len(left_children) != len(right_children):
            self.report(path + "/children", len(left_children), len(right_children))

        names = Counter()
        for left_child, right_child in zip(left_children, right_children):
            index = names[left_child.tag]
            names[left_child.tag] += 1
            label = left_child.findtext("DisplayName")
            suffix = f" ({label[:50]})" if label else ""
            child_path = f"{path}/{left_child.tag}[{index}]{suffix}"
            self.compare(left_child, right_child, child_path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", help="Baseline save XML or .sav file")
    parser.add_argument("after", help="Comparison save XML or .sav file")
    parser.add_argument("--max-diffs", type=int, default=30)
    args = parser.parse_args()
    if args.max_diffs < 1:
        parser.error("--max-diffs must be at least 1")

    differ = SaveDiff(args.max_diffs)
    try:
        before = ET.parse(args.before).getroot()
        after = ET.parse(args.after).getroot()
    except (OSError, ET.ParseError) as error:
        parser.exit(2, f"Cannot read save XML: {error}\n")

    differ.compare(before, after, "/" + before.tag)
    if differ.difference_count:
        print(f"Found {differ.difference_count} difference(s):")
        for path, left, right in differ.differences:
            print(f"  {path}: {left!r} != {right!r}")
        if differ.difference_count > len(differ.differences):
            print(f"  ... {differ.difference_count - len(differ.differences)} more")
        return 1

    print(
        "Equivalent after normalizing "
        f"{len(differ.left_to_right)} generated IDs "
        f"across {differ.id_references} references."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

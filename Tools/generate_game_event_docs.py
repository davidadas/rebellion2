#!/usr/bin/env python3
"""Generate the exhaustive game-event XML API reference from its authoritative contracts."""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCHEMA_PATH = ROOT / "Assets/Content/Application/Schemas/game-events.xsd"
TRIGGER_PATH = ROOT / "Assets/Scripts/Game/Events/GameEventTrigger.cs"
OUTPUT_PATH = ROOT / "Docs/GameEvents.API.md"
XSD = "{http://www.w3.org/2001/XMLSchema}"


def occurrence(node: ET.Element) -> str:
    minimum = node.get("minOccurs", "1")
    maximum = node.get("maxOccurs", "1")
    if minimum == maximum == "1":
        return "Exactly one"
    if minimum == "0" and maximum == "1":
        return "Optional"
    if maximum == "unbounded":
        return f"{minimum} or more"
    return f"{minimum}–{maximum}"


class Schema:
    def __init__(self, path: Path) -> None:
        self.root = ET.parse(path).getroot()
        self.complex_types = {
            node.get("name"): node
            for node in self.root.findall(f"{XSD}complexType")
            if node.get("name")
        }
        self.simple_types = {
            node.get("name"): node
            for node in self.root.findall(f"{XSD}simpleType")
            if node.get("name")
        }

    def attributes(self, type_name: str) -> list[ET.Element]:
        node = self.complex_types[type_name]
        attributes: list[ET.Element] = []
        extension = node.find(f".//{XSD}extension")
        if extension is not None and extension.get("base") in self.complex_types:
            attributes.extend(self.attributes(extension.get("base")))
        attributes.extend(node.findall(f"./{XSD}attribute"))
        if extension is not None:
            attributes.extend(extension.findall(f"./{XSD}attribute"))
        return attributes

    def direct_elements(self, type_name: str) -> list[ET.Element]:
        node = self.complex_types[type_name]
        extension = node.find(f".//{XSD}extension")
        elements: list[ET.Element] = []
        if extension is not None and extension.get("base") in self.complex_types:
            elements.extend(self.direct_elements(extension.get("base")))
        container = extension if extension is not None else node
        for compositor_name in ("sequence", "all", "choice"):
            compositor = container.find(f"./{XSD}{compositor_name}")
            if compositor is not None:
                for element in compositor.findall(f"./{XSD}element"):
                    rendered = ET.Element(element.tag, element.attrib)
                    if compositor_name == "choice" and compositor.get("maxOccurs") == "unbounded":
                        rendered.set("minOccurs", "0")
                        rendered.set("maxOccurs", "unbounded")
                    rendered.extend(list(element))
                    elements.append(rendered)
        return elements

    def choice_elements(self, type_name: str) -> list[ET.Element]:
        node = self.complex_types[type_name]
        choice = node.find(f"./{XSD}choice")
        if choice is not None:
            return choice.findall(f"./{XSD}element")
        sequence = node.find(f"./{XSD}sequence")
        if sequence is None:
            return []
        choice = sequence.find(f"./{XSD}choice")
        return [] if choice is None else choice.findall(f"./{XSD}element")

    def enum_values(self, type_name: str) -> list[str]:
        node = self.simple_types.get(type_name)
        if node is None:
            return []
        return [entry.get("value") for entry in node.findall(f".//{XSD}enumeration")]


def escape(value: str | None) -> str:
    return (value or "—").replace("|", "\\|")


def type_constraints(schema: Schema, type_name: str | None) -> str:
    if not type_name:
        return "Inline definition"
    values = schema.enum_values(type_name)
    if values:
        return ", ".join(f"`{value}`" for value in values)
    node = schema.simple_types.get(type_name)
    if node is None:
        return f"`{type_name}`"
    restriction = node.find(f"{XSD}restriction")
    if restriction is None:
        return f"`{type_name}`"
    details = [f"`{restriction.get('base')}`"]
    for constraint in restriction:
        label = constraint.tag.removeprefix(XSD)
        details.append(f"{label} `{constraint.get('value')}`")
    return "; ".join(details)


def inline_definition(element: ET.Element) -> str:
    complex_type = element.find(f"{XSD}complexType")
    if complex_type is None:
        return "Inline definition"
    nested = complex_type.findall(f"./*/{XSD}element")
    if not nested:
        return "Inline definition"
    members = ", ".join(
        f"`{child.get('name')}` ({occurrence(child)})" for child in nested
    )
    return f"Inline: {members}"


def render_type(schema: Schema, element_name: str, type_name: str) -> list[str]:
    lines = [f"### `{element_name}`", ""]
    attributes = schema.attributes(type_name)
    if attributes:
        lines.extend(
            [
                "Attributes:",
                "",
                "| Name | Required | Type or allowed values |",
                "| --- | --- | --- |",
            ]
        )
        for attribute in attributes:
            lines.append(
                f"| `{escape(attribute.get('name'))}` | "
                f"{'Yes' if attribute.get('use') == 'required' else 'No'} | "
                f"{type_constraints(schema, attribute.get('type'))} |"
            )
        lines.append("")
    children = schema.direct_elements(type_name)
    if children:
        lines.extend(
            [
                "Children:",
                "",
                "| Element | Occurrence | Type |",
                "| --- | --- | --- |",
            ]
        )
        for child in children:
            child_type = (
                type_constraints(schema, child.get("type"))
                if child.get("type")
                else inline_definition(child)
            )
            lines.append(
                f"| `{escape(child.get('name'))}` | {occurrence(child)} | "
                f"{child_type} |"
            )
        lines.append("")
    if not attributes and not children:
        lines.extend(["This element has no attributes or child elements.", ""])
    return lines


def parse_trigger_contracts(path: Path) -> list[tuple[str, str, list[tuple[str, str]]]]:
    source = path.read_text(encoding="utf-8")
    pattern = re.compile(
        r'Register<(?P<result>\w+)>\(contracts, "(?P<event>[^"]+)"\)'
        r'(?P<arguments>.*?);',
        re.DOTALL,
    )
    argument_pattern = re.compile(
        r'\.Argument\("(?P<name>[^"]+)",\s*result\s*=>\s*(?P<expression>.*?)\)',
        re.DOTALL,
    )
    contracts = []
    for match in pattern.finditer(source):
        arguments = [
            (argument.group("name"), " ".join(argument.group("expression").split()))
            for argument in argument_pattern.finditer(match.group("arguments"))
        ]
        contracts.append((match.group("event"), match.group("result"), arguments))
    return contracts


def render_reference(schema: Schema) -> str:
    lines = [
        "# Game event XML API reference",
        "",
        "> Generated by `Tools/generate_game_event_docs.py`. Do not edit this file manually.",
        "> Syntax comes from `game-events.xsd`; trigger arguments come from the runtime trigger registry.",
        "",
        "This reference lists every currently accepted event element and its serialized shape. See",
        "[Creating custom game events](GameEvents.md) for lifecycle semantics and complete recipes.",
        "",
        "## Event definition",
        "",
    ]
    lines.extend(render_type(schema, "GameEvent", "GameEventType"))

    lines.extend(["## Scheduling", ""])
    for element in schema.choice_elements("GameEventSchedulerType"):
        lines.extend(render_type(schema, element.get("name"), element.get("type")))

    lines.extend(["## Trigger contracts", ""])
    for event_id, result_type, arguments in parse_trigger_contracts(TRIGGER_PATH):
        lines.extend(
            [
                f"### `{event_id}`",
                "",
                f"Runtime result: `{result_type}`",
                "",
                "| Argument | Runtime value source |",
                "| --- | --- |",
            ]
        )
        for name, expression in arguments:
            lines.append(f"| `{name}` | `{escape(expression)}` |")
        lines.append("")

    sections = [
        ("Conditions", "ConditionalsType"),
        ("Selectors", "TargetType"),
        ("Actions", "ActionsType"),
    ]
    rendered: set[tuple[str, str]] = set()
    for heading, type_name in sections:
        lines.extend([f"## {heading}", ""])
        elements = schema.choice_elements(type_name)
        if heading == "Selectors":
            selector_names: dict[str, str] = {}
            for candidate_type in schema.complex_types:
                for element in schema.choice_elements(candidate_type):
                    if element.get("name", "").startswith("Select") and element.get("type"):
                        selector_names[element.get("name")] = element.get("type")
            elements = [
                ET.Element(f"{XSD}element", {"name": name, "type": selector_type})
                for name, selector_type in sorted(selector_names.items())
            ]
        for element in elements:
            key = (element.get("name"), element.get("type"))
            if key in rendered or not element.get("type"):
                continue
            rendered.add(key)
            lines.extend(render_type(schema, *key))

    lines.extend(["## Enumerated values", ""])
    for type_name in sorted(schema.simple_types):
        values = schema.enum_values(type_name)
        if not values:
            continue
        lines.extend(
            [
                f"### `{type_name}`",
                "",
                ", ".join(f"`{value}`" for value in values),
                "",
            ]
        )
    lines.extend(["## Named schema types", ""])
    lines.extend(
        [
            "These definitions expand the named payload types referenced by the element tables above.",
            "",
        ]
    )
    for type_name in sorted(schema.complex_types):
        lines.extend(render_type(schema, type_name, type_name))
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    missing = [path for path in (SCHEMA_PATH, TRIGGER_PATH) if not path.is_file()]
    if missing:
        print("Missing documentation input: " + ", ".join(str(path) for path in missing))
        return 1

    generated = render_reference(Schema(SCHEMA_PATH))
    if args.check:
        current = OUTPUT_PATH.read_text(encoding="utf-8") if OUTPUT_PATH.is_file() else ""
        if current != generated:
            print(f"Generated event documentation is stale: {OUTPUT_PATH.relative_to(ROOT)}")
            print("Run ./build.sh docs and commit the result.")
            return 1
        print("Generated event documentation is current.")
        return 0

    OUTPUT_PATH.write_text(generated, encoding="utf-8")
    print(f"Generated {OUTPUT_PATH.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Fail when a C# file, type, or method exceeds its size limit.

Files must stay small enough to read in a small slice of an LLM context window,
so oversized "god" files, types, and methods are rejected. Limits count physical
lines: 250 per C# file, 200 per type, 40 per method (signature through closing
brace, or through the semicolon of an expression-bodied member). Comments and
string contents are ignored when matching braces.
"""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
FILE_LIMIT = 250
TYPE_LIMIT = 200
METHOD_LIMIT = 40
SKIPPED = {"bin", "obj", "artifacts", ".git", "node_modules"}
TYPE_KEYWORD = re.compile(r"\b(class|struct|record|interface|enum)\b")
CONTROL_START = re.compile(
    r"^(if|else|for|foreach|while|switch|using|lock|try|catch|finally|do|fixed|checked|unchecked|"
    r"namespace|get|set|init|add|remove|unsafe|return|yield|throw)\b"
)


def source_files():
    for path in ROOT.rglob("*.cs"):
        if not SKIPPED.intersection(path.relative_to(ROOT).parts):
            yield path


def blank_literals(text):
    """Replace comments and string/char literal contents with spaces, keeping newlines."""
    out = []
    i, n = 0, len(text)
    while i < n:
        two = text[i : i + 2]
        if two == "//":
            while i < n and text[i] != "\n":
                out.append(" ")
                i += 1
        elif two == "/*":
            while i < n and text[i : i + 2] != "*/":
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            out.append("  ")
            i += 2
        elif text[i] == '"' or text[i : i + 2] in ('@"', '$"') or text[i : i + 3] in ('$@"', '@$"'):
            i = blank_string(text, i, out)
        elif text[i] == "'":
            i = blank_char(text, i, out)
        else:
            out.append(text[i])
            i += 1
    return "".join(out)


def blank_string(text, i, out):
    verbatim = "@" in text[i : i + 3].split('"')[0]
    raw = text[i : i + 3] == '"""'
    while text[i] != '"':
        out.append(" ")
        i += 1
    if raw:
        end = text.find('"""', i + 3)
        end = len(text) if end < 0 else end + 3
        out.extend("\n" if c == "\n" else " " for c in text[i:end])
        return end
    out.append('"')
    i += 1
    while i < len(text):
        c = text[i]
        if c == "\\" and not verbatim:
            out.append("  ")
            i += 2
            continue
        if c == '"':
            if verbatim and text[i : i + 2] == '""':
                out.append("  ")
                i += 2
                continue
            out.append('"')
            return i + 1
        out.append("\n" if c == "\n" else " ")
        i += 1
    return i


def blank_char(text, i, out):
    end = i + 1
    while end < len(text) and text[end] != "'":
        end += 2 if text[end] == "\\" else 1
    out.append("'" + " " * (end - i - 1) + "'")
    return end + 1


def members(clean):
    """Yield (kind, first_line, last_line) for every type and method found."""
    stack = []
    header_start = 0
    paren = 0
    in_type_depth = []
    for index, char in enumerate(clean):
        if char == "(":
            paren += 1
        elif char == ")":
            paren -= 1
        elif char == "{" and paren == 0:
            header = clean[header_start:index]
            kind = classify(header, bool(in_type_depth) and in_type_depth[-1] == len(stack))
            first = line_of(clean, header_start, header)
            stack.append((kind, first))
            if kind == "type":
                in_type_depth.append(len(stack))
            header_start = index + 1
        elif char == "}" and paren == 0:
            kind, first = stack.pop()
            if kind == "type":
                in_type_depth.pop()
            if kind in ("type", "method"):
                yield kind, first, clean.count("\n", 0, index) + 1
            header_start = index + 1
        elif char == ";" and paren == 0:
            header = clean[header_start:index]
            inside_type = bool(in_type_depth) and in_type_depth[-1] == len(stack)
            if inside_type and "=>" in header and "(" in header.split("=>")[0]:
                first = line_of(clean, header_start, header)
                yield "method", first, clean.count("\n", 0, index) + 1
            header_start = index + 1


def line_of(clean, start, header):
    lead = len(header) - len(header.lstrip())
    return clean.count("\n", 0, start + lead) + 1


def classify(header, directly_in_type):
    text = " ".join(header.split())
    if not text:
        return "other"
    head = text.split("(")[0]
    if TYPE_KEYWORD.search(head) and "=>" not in head and "=" not in head.replace("==", ""):
        return "type"
    if CONTROL_START.match(text) or text.endswith(("=", "=>")) or "=>" in text:
        return "other"
    if directly_in_type and "(" in text and ")" in text:
        return "method"
    return "other"


failures = []
for path in source_files():
    text = path.read_text(encoding="utf-8")
    relative = path.relative_to(ROOT)
    count = text.count("\n") + (0 if text.endswith("\n") else 1)
    if count > FILE_LIMIT:
        failures.append((f"{relative}: {count} lines (file limit {FILE_LIMIT})"))
    for kind, first, last in members(blank_literals(text)):
        length = last - first + 1
        limit = TYPE_LIMIT if kind == "type" else METHOD_LIMIT
        if length > limit:
            failures.append(f"{relative}:{first}: {kind} is {length} lines (limit {limit})")

for failure in sorted(failures):
    print(failure)
if failures:
    print(f"{len(failures)} size violation(s). Split by responsibility.")
    sys.exit(1)
print("All files, types, and methods are within the size limits.")

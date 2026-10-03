#!/usr/bin/env python3
"""Navigate a Theoriarr diagnostics dump (a single huge .txt) without loading it.

The dump written by NzbDrone.Core/Diagnostics/DiagnosticsService.cs is tens to
hundreds of MB, so opening it in an editor or grepping the whole thing wastes
time and context. This tool indexes the section headers once and then seeks to
just the part you ask for.

Usage:
  dump-nav.py DUMP sections                 list sections (offset + byte size)
  dump-nav.py DUMP show SECTION             print a section
  dump-nav.py DUMP grep SECTION PATTERN     regex search inside one section (-i, -n)
  dump-nav.py DUMP tables [SECTION]         list database tables (name, rows)
  dump-nav.py DUMP table NAME [--limit N]   print one table's rows
  dump-nav.py DUMP files [SECTION]          list "FILE:" entries in a section
  dump-nav.py DUMP json SECTION             pretty-print a JSON section
  dump-nav.py DUMP head SECTION [N]         first N lines of a section (default 40)
  dump-nav.py DUMP tail SECTION [N]         last N lines of a section (default 40)

SECTION is a case-insensitive regex matched against the section title
(e.g. "download queue", "database: theoriarr", "config", "log files").
An index is cached next to the dump as "<dump>.nav.json" (keyed on size+mtime).
"""
import argparse
import json
import os
import re
import signal
import sys

SECTION = re.compile(r"^={10,} (.+?) ={10,}$")
FILE_MARK = re.compile(r"^=+ FILE: (.+?) =+$")
TABLE_MARK = re.compile(r"^--- (.+?) \((\d+) rows?\) ---$")


def build_index(path):
    size = os.path.getsize(path)
    mtime = os.path.getmtime(path)
    cache = path + ".nav.json"

    if os.path.exists(cache):
        try:
            with open(cache, "r", encoding="utf-8") as fh:
                data = json.load(fh)
            if data.get("size") == size and data.get("mtime") == mtime:
                return data["sections"], size
        except (ValueError, KeyError, OSError):
            pass

    sections = []
    current = None

    with open(path, "rb") as fh:
        offset = 0
        while True:
            line_off = offset
            raw = fh.readline()
            if not raw:
                break
            offset += len(raw)
            line = raw.decode("utf-8", "replace").rstrip("\r\n")

            match = SECTION.match(line)
            if match:
                if current is not None:
                    current["end"] = line_off
                    sections.append(current)
                current = {
                    "title": match.group(1),
                    "start": line_off,
                    "body": offset,
                    "end": None,
                }

    if current is not None:
        current["end"] = size
        sections.append(current)

    for sec in sections:
        sec.setdefault("end", size)

    try:
        with open(cache, "w", encoding="utf-8") as fh:
            json.dump({"size": size, "mtime": mtime, "sections": sections}, fh)
    except OSError:
        pass

    return sections, size


def iter_region(path, start, end):
    with open(path, "rb") as fh:
        fh.seek(start)
        while True:
            pos = fh.tell()
            if pos >= end:
                break
            raw = fh.readline()
            if not raw:
                break
            yield pos, raw.decode("utf-8", "replace").rstrip("\r\n")


def resolve(sections, query):
    try:
        pattern = re.compile(query, re.IGNORECASE)
    except re.error:
        pattern = re.compile(re.escape(query), re.IGNORECASE)
    return [s for s in sections if pattern.search(s["title"])]


def cmd_sections(path, sections, args):
    width = max(len(s["title"]) for s in sections) if sections else 0
    for s in sections:
        size = s["end"] - s["start"]
        yield f"{s['start']:>12}  {size:>12}  {s['title']:<{width}}"


def cmd_show(path, sections, args):
    matches = resolve(sections, args.section)
    if not matches:
        yield f"no section matches {args.section!r}"
        return
    limit = args.limit
    for s in matches:
        body = s["body"]
        yield f"===== {s['title']} ====="
        if body is None:
            yield "(header only)"
        else:
            for i, (_, line) in enumerate(iter_region(path, body, s["end"])):
                if limit is not None and i >= limit:
                    yield f"... ({i} lines shown; use --limit 0 for all)"
                    break
                yield line


def cmd_grep(path, sections, args):
    matches = resolve(sections, args.section)
    if not matches:
        yield f"no section matches {args.section!r}"
        return
    flags = re.IGNORECASE if args.ignore_case else 0
    try:
        pattern = re.compile(args.pattern, flags)
    except re.error as exc:
        yield f"bad pattern: {exc}"
        return
    limit = args.limit
    shown = 0
    for s in matches:
        for _, line in iter_region(path, s["body"], s["end"]):
            if pattern.search(line):
                shown += 1
                if limit and shown > limit:
                    yield f"... (stopped after {limit} matches)"
                    return
                yield line


def _iter_tables(path, sections):
    for s in sections:
        if not s["title"].upper().startswith("DATABASE"):
            continue
        for _, line in iter_region(path, s["body"], s["end"]):
            m = TABLE_MARK.match(line)
            if m:
                yield s, m.group(1), int(m.group(2))


def cmd_tables(path, sections, args):
    scope = resolve(sections, args.section) if args.section else sections
    found = False
    for sec, name, rows in _iter_tables(path, scope):
        found = True
        yield f"{rows:>10}  {name}  [{sec['title']}]"
    if not found:
        yield "no tables found (is the section a DATABASE one?)"


def cmd_table(path, sections, args):
    in_table = False
    limit = args.limit
    count = 0
    for sec in sections:
        if not sec["title"].upper().startswith("DATABASE"):
            continue
        for _, line in iter_region(path, sec["body"], sec["end"]):
            m = TABLE_MARK.match(line)
            if m:
                if in_table:
                    return
                if m.group(1) == args.name:
                    in_table = True
                    yield f"===== {m.group(1)} ({m.group(2)} rows) ====="
                continue
            if in_table:
                if line.strip() == "":
                    continue
                count += 1
                if limit is not None and count > limit:
                    yield f"... (stopped after {limit} rows)"
                    return
                yield line
    if not in_table:
        yield f"table {args.name!r} not found"


def cmd_files(path, sections, args):
    scope = resolve(sections, args.section) if args.section else sections
    found = False
    for s in scope:
        for _, line in iter_region(path, s["body"], s["end"]):
            m = FILE_MARK.match(line)
            if m:
                found = True
                yield f"[{s['title']}] {m.group(1)}"
    if not found:
        yield "no FILE: entries in the selected section(s)"


def cmd_json(path, sections, args):
    matches = resolve(sections, args.section)
    if not matches:
        yield f"no section matches {args.section!r}"
        return
    for s in matches:
        text = "\n".join(line for _, line in iter_region(path, s["body"], s["end"]))
        try:
            yield json.dumps(json.loads(text), indent=2)
        except ValueError as exc:
            yield f"(not JSON: {exc})"


def cmd_head(path, sections, args):
    args.limit = args.n
    yield from cmd_show(path, sections, args)


def cmd_tail(path, sections, args):
    matches = resolve(sections, args.section)
    if not matches:
        yield f"no section matches {args.section!r}"
        return
    for s in matches:
        lines = [line for _, line in iter_region(path, s["body"], s["end"])]
        yield f"===== {s['title']} ====="
        for line in lines[-args.n:]:
            yield line


def main(argv=None):
    if hasattr(signal, "SIGPIPE"):
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)

    parser = argparse.ArgumentParser(
        description="Navigate a Theoriarr diagnostics dump.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("dump", help="path to the diagnostics .txt")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("sections", help="list sections with byte offset and size")

    p = sub.add_parser("show", help="print a section (--limit 0 = all)")
    p.add_argument("section")
    p.add_argument("--limit", type=int, default=0, help="max lines, 0 = all")

    p = sub.add_parser("head", help="first N lines of a section")
    p.add_argument("section")
    p.add_argument("n", nargs="?", type=int, default=40)

    p = sub.add_parser("tail", help="last N lines of a section")
    p.add_argument("section")
    p.add_argument("n", nargs="?", type=int, default=40)

    p = sub.add_parser("grep", help="regex search inside one section")
    p.add_argument("section")
    p.add_argument("pattern")
    p.add_argument("-i", "--ignore-case", action="store_true")
    p.add_argument("--limit", type=int, default=0, help="max matches, 0 = all")

    p = sub.add_parser("tables", help="list database tables")
    p.add_argument("section", nargs="?", default=None)

    p = sub.add_parser("table", help="print one table's rows")
    p.add_argument("name")
    p.add_argument("--limit", type=int, default=0, help="max rows, 0 = all")

    p = sub.add_parser("files", help="list FILE: entries in a section")
    p.add_argument("section", nargs="?", default=None)

    p = sub.add_parser("json", help="pretty-print a JSON section")
    p.add_argument("section")

    args = parser.parse_args(argv)
    if not os.path.isfile(args.dump):
        parser.error(f"no such file: {args.dump}")

    if getattr(args, "limit", 0) == 0:
        args.limit = None

    sections, _ = build_index(args.dump)

    commands = {
        "sections": cmd_sections,
        "show": cmd_show,
        "head": cmd_head,
        "tail": cmd_tail,
        "grep": cmd_grep,
        "tables": cmd_tables,
        "table": cmd_table,
        "files": cmd_files,
        "json": cmd_json,
    }
    for line in commands[args.cmd](args.dump, sections, args):
        print(line)


if __name__ == "__main__":
    main()

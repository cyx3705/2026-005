from __future__ import annotations

import argparse
import re
from pathlib import Path


ALLOWED = re.compile(
    r"[\u4e00-\u9fffA-Za-z0-9\u00b0\u00b1\u03c0\u03c6\u0394"
    r"。，、；：！？（）()《》【】\[\]—+×÷/=<>%·.,:;_ \-]+"
)


def recover_runs(path: Path) -> list[str]:
    data = path.read_bytes()
    decoded = data.decode("utf-16le", errors="ignore")
    runs: list[str] = []
    for match in ALLOWED.finditer(decoded):
        value = re.sub(r"\s+", " ", match.group()).strip()
        if len(value) >= 2 and (re.search(r"[\u4e00-\u9fff]", value) or len(value) >= 4):
            runs.append(value)
    return runs


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument("--start", type=int, default=0)
    parser.add_argument("--end", type=int)
    args = parser.parse_args()
    runs = recover_runs(args.path)
    end = args.end if args.end is not None else len(runs)
    print(f"RUN_COUNT={len(runs)}")
    for index, value in enumerate(runs[args.start:end], start=args.start):
        print(f"{index:04d}: {value}")


if __name__ == "__main__":
    main()

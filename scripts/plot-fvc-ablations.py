#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Plot actual ablation decisions without fingerprint images or new measurements.

Usage: plot-fvc-ablations.py REPORT.json OUTPUT_BASENAME
"""

import json
import os
from pathlib import Path
import sys

os.environ.setdefault("MPLCONFIGDIR", "/private/tmp/openphysical-matplotlib-cache")
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


def plot(report_path: Path, output: Path) -> int:
    report = json.loads(report_path.read_text())
    rows = report["Decisions"]
    names = {
        "RawWhole": "Raw + whole\ncomponent",
        "RawRetained": "Raw + retained\nregion",
        "RawInner": "Raw + inner\nregion",
        "MaskedOnly": "Mask only",
        "ClaheOnly": "CLAHE only",
        "GainOnly": "Gain only",
        "Full": "CLAHE + gain",
    }
    values = [row["GenuineAccepted"] for row in rows]
    colors = ["#64748b"] + ["#8abeb5"] * (len(rows) - 1)
    colors[values.index(max(values))] = "#00796b"
    plt.rcParams.update({"font.size": 12, "svg.fonttype": "none", "figure.facecolor": "white"})
    figure, axis = plt.subplots(figsize=(12, 5.8))
    bars = axis.bar(range(len(rows)), values, width=0.68, color=colors, zorder=3)
    axis.axhline(values[0], color="#64748b", linestyle="--", linewidth=1)
    axis.set_ylim(0, max(values) * 1.17)
    axis.set_xticks(range(len(rows)), [names[row["Variant"]] for row in rows])
    axis.set_ylabel(f"Genuine accepts out of {rows[0]['GenuineTotal']} attempts", labelpad=12)
    axis.tick_params(axis="both", length=0, pad=9)
    axis.grid(axis="y", color="#e6ebed", zorder=0)
    for spine in axis.spines.values():
        spine.set_visible(False)
    for bar, value in zip(bars, values):
        axis.text(bar.get_x() + bar.get_width() / 2, value + 4, str(value), ha="center", fontsize=14)
    figure.suptitle(f"Development ablations at fixed Bozorth threshold {report['Threshold']}",
                   x=0.075, y=0.97, ha="left", fontsize=18)
    cohort = report["CohortManifest"]["cohort"]
    figure.text(0.075, 0.90, f"{cohort} · optical 500 ppi · not final validation", color="#4b5563")
    if len({(row['GenuineAvailable'], row['GenuineTotal']) for row in rows}) == 1:
        row = rows[0]
        figure.text(0.075, 0.855, f"Genuine comparisons: {row['GenuineAvailable']} scored + "
                    f"{row['GenuineTotal'] - row['GenuineAvailable']} unavailable = {row['GenuineTotal']} total attempts.",
                    fontsize=11, color="#4b5563")
    if len({(row['ImpostorAccepted'], row['ImpostorAvailable'], row['ImpostorTotal']) for row in rows}) == 1:
        row = rows[0]
        figure.text(0.075, 0.095, f"All variants: {row['ImpostorAccepted']} impostor accepts / "
                    f"{row['ImpostorAvailable']:,} scored comparisons; {row['ImpostorTotal'] - row['ImpostorAvailable']} unavailable attempts.")
    figure.text(0.075, 0.045, "Unavailable attempts remain in total denominators. "
                "Paired images are dependent; this is not a population accuracy estimate.", fontsize=10, color="#4b5563")
    figure.subplots_adjust(left=0.09, right=0.98, top=0.81, bottom=0.23)
    output.parent.mkdir(parents=True, exist_ok=True)
    for suffix in [".png", ".svg"]:
        figure.savefig(output.with_suffix(suffix), dpi=180)
    plt.close(figure)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
    sys.exit(plot(Path(sys.argv[1]), Path(sys.argv[2])))

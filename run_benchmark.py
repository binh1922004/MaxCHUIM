#!/usr/bin/env python3
"""
Benchmark runner for MaxCHUIM algorithms.

Reads a YAML config file, builds the .NET project once, then runs each
(algorithm × dataset × threshold × attempt) combination, parses the
structured output, and writes all results to a timestamped CSV file.

Usage:
    python3 run_benchmark.py                          # uses benchmark_config.yml
    python3 run_benchmark.py --config my_config.yml   # custom config path
"""

import argparse
import csv
import os
import subprocess
import sys
from datetime import datetime

try:
    import yaml
except ImportError:
    print("ERROR: PyYAML is required. Install it with: pip3 install pyyaml")
    sys.exit(1)


# ─── CSV columns ───
CSV_COLUMNS = [
    "algorithm",
    "dataset",
    "threshold",
    "mu",
    "total_twu",
    "attempt",
    "runtime_ms",
    "memory_kb",
    "managed_memory_kb",
    "total_rss_kb",
    "closed_hui_count",
    "max_hui_count",
    "candidates_count",
    "max_hui_checks",
]


def load_config(config_path: str) -> dict:
    """Load and validate the YAML config file."""
    if not os.path.exists(config_path):
        print(f"ERROR: Config file not found: {config_path}")
        sys.exit(1)

    with open(config_path, "r") as f:
        config = yaml.safe_load(f)

    # Validate required keys
    required_keys = ["hui_folder", "pro_folder", "run_attempts", "algorithms", "datasets"]
    for key in required_keys:
        if key not in config:
            print(f"ERROR: Missing required config key: '{key}'")
            sys.exit(1)

    return config


def build_project(project_dir: str) -> bool:
    """Build the .NET project in Release mode."""
    print("=" * 60)
    print("Building project in Release mode...")
    print("=" * 60)

    result = subprocess.run(
        ["dotnet", "build", "-c", "Release"],
        cwd=project_dir,
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        print(f"BUILD FAILED!\n{result.stderr}")
        return False

    print("Build succeeded.\n")
    return True


def run_single_benchmark(
    project_dir: str,
    algorithm: str,
    hui_path: str,
    pro_path: str,
    threshold: float,
) -> dict | None:
    """
    Run a single benchmark invocation and parse the BENCHMARK_RESULT line.
    Returns a dict of parsed fields, or None if parsing failed.
    No timeout — runs until completion.
    """
    cmd = [
        "dotnet", "run",
        "-c", "Release",
        "--no-build",
        "--",
        "--benchmark",
        "--algorithm", algorithm,
        "--hui", hui_path,
        "--pro", pro_path,
        "--threshold", str(threshold),
    ]

    result = subprocess.run(
        cmd,
        cwd=project_dir,
        capture_output=True,
        text=True,
        timeout=None,  # No timeout
    )

    if result.returncode != 0:
        print(f"  [ERROR] Process exited with code {result.returncode}")
        if result.stderr:
            # Print first 5 lines of stderr to avoid flooding
            for line in result.stderr.strip().split("\n")[:5]:
                print(f"    stderr: {line}")
        return None

    # Parse the BENCHMARK_RESULT line from stdout
    for line in result.stdout.strip().split("\n"):
        if line.startswith("BENCHMARK_RESULT|"):
            return parse_benchmark_line(line)

    print(f"  [ERROR] No BENCHMARK_RESULT line found in output")
    if result.stdout:
        for line in result.stdout.strip().split("\n")[:5]:
            print(f"    stdout: {line}")
    return None


def parse_benchmark_line(line: str) -> dict:
    """Parse a pipe-delimited BENCHMARK_RESULT line into a dict."""
    parts = line.split("|")
    data = {}
    for part in parts[1:]:  # skip "BENCHMARK_RESULT"
        key, value = part.split("=", 1)
        data[key] = value
    return data


def main():
    parser = argparse.ArgumentParser(description="Benchmark runner for MaxCHUIM algorithms")
    parser.add_argument(
        "--config",
        default="benchmark_config.yml",
        help="Path to the YAML config file (default: benchmark_config.yml)",
    )
    args = parser.parse_args()

    # Resolve paths relative to this script's directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    config_path = os.path.join(script_dir, args.config) if not os.path.isabs(args.config) else args.config
    project_dir = os.path.join(script_dir, "MaxCHUIM")

    # Load config
    config = load_config(config_path)
    hui_folder = config["hui_folder"]
    pro_folder = config["pro_folder"]
    run_attempts = config["run_attempts"]
    algorithms = config["algorithms"]
    datasets = config["datasets"]

    # Build once
    if not build_project(project_dir):
        sys.exit(1)

    # Prepare output CSV
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    csv_filename = f"benchmark_results_{timestamp}.csv"
    csv_path = os.path.join(script_dir, "results", csv_filename)

    # Count total runs for progress
    total_runs = len(algorithms) * sum(len(d["thresholds"]) for d in datasets) * run_attempts
    current_run = 0

    print(f"Starting benchmark: {total_runs} total runs")
    print(f"Results will be saved to: {csv_path}\n")

    with open(csv_path, "w", newline="") as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=CSV_COLUMNS)
        writer.writeheader()

        for dataset_cfg in datasets:
            dataset_name = dataset_cfg["name"]
            hui_path = os.path.join(hui_folder, f"{dataset_name}.hui")
            pro_path = os.path.join(pro_folder, f"{dataset_name}.pro")

            # Verify dataset files exist
            if not os.path.exists(hui_path):
                print(f"[WARNING] HUI file not found, skipping: {hui_path}")
                continue
            if not os.path.exists(pro_path):
                print(f"[WARNING] PRO file not found, skipping: {pro_path}")
                continue

            for threshold in dataset_cfg["thresholds"]:
                for algorithm in algorithms:
                    for attempt in range(1, run_attempts + 1):
                        current_run += 1
                        progress = f"[{current_run}/{total_runs}]"
                        print(
                            f"{progress} {algorithm} | {dataset_name} | "
                            f"threshold={threshold} | attempt {attempt}/{run_attempts}",
                            end="",
                            flush=True,
                        )

                        parsed = run_single_benchmark(
                            project_dir=project_dir,
                            algorithm=algorithm,
                            hui_path=hui_path,
                            pro_path=pro_path,
                            threshold=threshold,
                        )

                        if parsed:
                            row = {
                                "algorithm": parsed.get("algorithm", algorithm),
                                "dataset": parsed.get("dataset", dataset_name),
                                "threshold": parsed.get("threshold", threshold),
                                "mu": parsed.get("mu", ""),
                                "total_twu": parsed.get("total_twu", ""),
                                "attempt": attempt,
                                "runtime_ms": parsed.get("runtime_ms", ""),
                                "memory_kb": parsed.get("memory_kb", ""),
                                "managed_memory_kb": parsed.get("managed_memory_kb", ""),
                                "total_rss_kb": parsed.get("total_rss_kb", ""),
                                "closed_hui_count": parsed.get("closed_hui_count", ""),
                                "max_hui_count": parsed.get("max_hui_count", ""),
                                "candidates_count": parsed.get("candidates_count", ""),
                                "max_hui_checks": parsed.get("max_hui_checks", ""),
                            }
                            writer.writerow(row)
                            csvfile.flush()  # Flush after each row so partial results are saved
                            runtime = parsed.get("runtime_ms", "?")
                            memory = parsed.get("total_rss_kb", "?")
                            print(f" → {runtime} ms, {memory} KB peak")
                        else:
                            # Write error row
                            row = {
                                "algorithm": algorithm,
                                "dataset": dataset_name,
                                "threshold": threshold,
                                "mu": "",
                                "total_twu": "",
                                "attempt": attempt,
                                "runtime_ms": "ERROR",
                                "memory_kb": "ERROR",
                                "managed_memory_kb": "ERROR",
                                "total_rss_kb": "ERROR",
                                "closed_hui_count": "ERROR",
                                "max_hui_count": "ERROR",
                                "candidates_count": "ERROR",
                                "max_hui_checks": "ERROR",
                            }
                            writer.writerow(row)
                            csvfile.flush()
                            print(" → ERROR")

    print(f"\n{'=' * 60}")
    print(f"Benchmark complete! Results saved to: {csv_path}")
    print(f"Total runs: {total_runs}")
    print(f"{'=' * 60}")


if __name__ == "__main__":
    main()

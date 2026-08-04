from pathlib import Path
import argparse

import matplotlib.pyplot as plt
import pandas as pd


def generate_line_charts(
    csv_path: str,
    output_dir: str = "charts",
    memory_column: str = "memory_kb",
) -> None:
    csv_file = Path(csv_path)
    chart_directory = Path(output_dir)

    if not csv_file.exists():
        raise FileNotFoundError(f"CSV file not found: {csv_file}")

    chart_directory.mkdir(parents=True, exist_ok=True)

    df = pd.read_csv(csv_file)

    required_columns = {
        "algorithm",
        "dataset",
        "threshold",
        "runtime_ms",
        memory_column,
    }

    missing_columns = required_columns - set(df.columns)

    if missing_columns:
        raise ValueError(
            f"Missing required columns: {', '.join(sorted(missing_columns))}"
        )

    numeric_columns = [
        "threshold",
        "runtime_ms",
        memory_column,
    ]

    for column in numeric_columns:
        df[column] = pd.to_numeric(df[column], errors="coerce")

    df = df.dropna(
        subset=[
            "algorithm",
            "dataset",
            "threshold",
            "runtime_ms",
            memory_column,
        ]
    ).copy()

    if df.empty:
        raise ValueError("The CSV does not contain valid chart data.")

    # Convert units.
    df["runtime_seconds"] = df["runtime_ms"] / 1000
    df["memory_mb"] = df[memory_column] / 1024

    for dataset_name, dataset_df in df.groupby("dataset", sort=True):
        safe_dataset_name = "".join(
            char if char.isalnum() or char in "-_" else "_"
            for char in str(dataset_name)
        )

        # ---------------------------------------------------------
        # 1. Generate and save the Runtime chart
        # ---------------------------------------------------------
        fig_runtime, ax_runtime = plt.subplots(figsize=(10, 6))

        for algorithm_name, algorithm_df in dataset_df.groupby("algorithm"):
            algorithm_df = algorithm_df.sort_values("threshold")
            ax_runtime.plot(
                algorithm_df["threshold"],
                algorithm_df["runtime_seconds"],
                marker="o",
                label=algorithm_name,
            )

        ax_runtime.set_title(f"Runtime comparison — {dataset_name}")
        ax_runtime.set_xlabel("Threshold")
        ax_runtime.set_ylabel("Runtime (seconds)")
        ax_runtime.grid(True, linestyle="--", alpha=0.5)
        ax_runtime.legend(title="Algorithm")

        fig_runtime.tight_layout()
        runtime_output_path = chart_directory / f"{safe_dataset_name}_runtime_chart.png"
        
        fig_runtime.savefig(
            runtime_output_path,
            dpi=200,
            bbox_inches="tight",
        )
        plt.close(fig_runtime)
        print(f"Created: {runtime_output_path}")


        # ---------------------------------------------------------
        # 2. Generate and save the Memory chart
        # ---------------------------------------------------------
        fig_memory, ax_memory = plt.subplots(figsize=(10, 6))

        for algorithm_name, algorithm_df in dataset_df.groupby("algorithm"):
            algorithm_df = algorithm_df.sort_values("threshold")
            ax_memory.plot(
                algorithm_df["threshold"],
                algorithm_df["memory_mb"],
                marker="o",
                label=algorithm_name,
            )

        ax_memory.set_title(f"Memory comparison — {dataset_name}")
        ax_memory.set_xlabel("Threshold")
        ax_memory.set_ylabel("Memory (MB)")
        ax_memory.grid(True, linestyle="--", alpha=0.5)
        ax_memory.legend(title="Algorithm")

        fig_memory.tight_layout()
        memory_output_path = chart_directory / f"{safe_dataset_name}_memory_chart.png"
        
        fig_memory.savefig(
            memory_output_path,
            dpi=200,
            bbox_inches="tight",
        )
        plt.close(fig_memory)
        print(f"Created: {memory_output_path}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate runtime and memory line charts."
    )

    parser.add_argument(
        "csv_file",
        help="Path to the input CSV file",
    )

    parser.add_argument(
        "--output-dir",
        default="charts",
        help="Directory for generated charts",
    )

    parser.add_argument(
        "--memory-column",
        default="memory_kb",
        choices=[
            "memory_kb",
        ],
        help="Memory column to display",
    )

    args = parser.parse_args()

    try:
        generate_line_charts(
            csv_path=args.csv_file,
            output_dir=args.output_dir,
            memory_column=args.memory_column,
        )
    except (FileNotFoundError, ValueError, pd.errors.ParserError) as error:
        print(f"Error: {error}")
        raise SystemExit(1)


if __name__ == "__main__":
    main()
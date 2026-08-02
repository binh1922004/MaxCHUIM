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
        fig, axes = plt.subplots(
            nrows=2,
            ncols=1,
            figsize=(12, 10),
        )

        for algorithm_name, algorithm_df in dataset_df.groupby("algorithm"):
            algorithm_df = algorithm_df.sort_values("threshold")

            # Runtime line.
            axes[0].plot(
                algorithm_df["threshold"],
                algorithm_df["runtime_seconds"],
                marker="o",
                label=algorithm_name,
            )

            # Memory line.
            axes[1].plot(
                algorithm_df["threshold"],
                algorithm_df["memory_mb"],
                marker="o",
                label=algorithm_name,
            )

        axes[0].set_title(f"Runtime comparison — {dataset_name}")
        axes[0].set_xlabel("Threshold")
        axes[0].set_ylabel("Runtime (seconds)")
        axes[0].grid(True, linestyle="--", alpha=0.5)
        axes[0].legend(title="Algorithm")

        axes[1].set_title(f"Memory comparison — {dataset_name}")
        axes[1].set_xlabel("Threshold")
        axes[1].set_ylabel("Memory (MB)")
        axes[1].grid(True, linestyle="--", alpha=0.5)
        axes[1].legend(title="Algorithm")

        fig.tight_layout()

        safe_dataset_name = "".join(
            char if char.isalnum() or char in "-_" else "_"
            for char in str(dataset_name)
        )

        output_path = (
            chart_directory / f"{safe_dataset_name}_line_chart.png"
        )

        fig.savefig(
            output_path,
            dpi=200,
            bbox_inches="tight",
        )

        plt.close(fig)

        print(f"Created: {output_path}")


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
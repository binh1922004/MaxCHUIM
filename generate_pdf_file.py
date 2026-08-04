import argparse
import sys
from pathlib import Path
from PIL import Image


def create_pdf_report(
    input_dir: str,
    chart_type: str,
    output_pdf: str
) -> None:
    img_directory = Path(input_dir)

    if not img_directory.exists():
        print(f"Error: Directory '{input_dir}' does not exist.")
        sys.exit(1)

    # Match the file naming convention from the previous script
    search_pattern = f"*_{chart_type}_chart.png"
    image_paths = sorted(img_directory.glob(search_pattern))

    if not image_paths:
        print(f"No images found matching '{search_pattern}' in '{input_dir}'.")
        sys.exit(1)

    print(f"Found {len(image_paths)} {chart_type} charts. Generating PDF...")

    image_list = []
    for img_path in image_paths:
        try:
            img = Image.open(img_path)
            # PDFs do not support RGBA (transparency) natively through Pillow, 
            # so we must convert images to RGB.
            img = img.convert("RGB")
            image_list.append(img)
        except Exception as e:
            print(f"Failed to process {img_path.name}: {e}")

    if not image_list:
        print("No valid images could be processed.")
        sys.exit(1)

    # The first image acts as the base for the PDF
    base_image = image_list[0]
    appended_images = image_list[1:]

    output_path = Path(output_pdf)
    
    # Save the base image and append the rest
    base_image.save(
        output_path,
        format="PDF",
        resolution=200.0,
        save_all=True,
        append_images=appended_images
    )

    print(f"Successfully created PDF report: {output_path.absolute()}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Compile runtime or memory charts into a single PDF report."
    )

    parser.add_argument(
        "--chart-type",
        required=True,
        choices=["runtime", "memory"],
        help="Type of charts to collect ('runtime' or 'memory')",
    )

    parser.add_argument(
        "--input-dir",
        default="charts",
        help="Directory where the chart images are stored (default: 'charts')",
    )

    parser.add_argument(
        "--output",
        default="chart_report.pdf",
        help="Name of the output PDF file (default: 'chart_report.pdf')",
    )

    args = parser.parse_args()

    create_pdf_report(
        input_dir=args.input_dir,
        chart_type=args.chart_type,
        output_pdf=args.output,
    )


if __name__ == "__main__":
    main()
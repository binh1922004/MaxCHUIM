import pandas as pd
import argparse
import sys

def main():
    # 1. Set up the argument parser
    parser = argparse.ArgumentParser(description="Calculate average metrics from a given CSV file.")
    
    # Add an argument for the input file path (required)
    parser.add_argument("filepath", help="Path to the input CSV file")
    
    # Add an optional argument for the output file name
    parser.add_argument("--output", default="averaged_results.csv", 
                        help="Path for the output CSV file (default: averaged_results.csv)")
    
    # Parse the arguments provided by the user
    args = parser.parse_args()

    # 2. Read the data from the provided file path
    try:
        df = pd.read_csv(args.filepath)
    except FileNotFoundError:
        print(f"Error: The file '{args.filepath}' was not found. Please check the path and try again.")
        sys.exit(1)
    except Exception as e:
        print(f"An error occurred while reading the file: {e}")
        sys.exit(1)

    # 3. Define the columns to process
    cols_to_avg = ['runtime_ms', 'memory_kb', 'managed_memory_kb', 'total_rss_kb', 
                   'closed_hui_count', 'max_hui_count', 'candidates_count', 'max_hui_checks']

    group_cols = ['algorithm', 'dataset', 'threshold', 'mu', 'total_twu']

    # 4. Calculate the averages
    print(f"Processing data from '{args.filepath}'...")
    avg_df = df.groupby(group_cols)[cols_to_avg].mean().reset_index()

    # 5. Save the results
    avg_df.to_csv(args.output, index=False)
    print(f"Success! Averages have been calculated and saved to '{args.output}'.")

if __name__ == "__main__":
    main()

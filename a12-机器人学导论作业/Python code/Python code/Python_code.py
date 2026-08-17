"""
Entry point for the wheeled-robot path-following reproduction.
Run: py Python_code.py
"""

from wheeled_robot_simulation import run_all_experiments, plot_error_comparison


if __name__ == "__main__":
    results = run_all_experiments()
    plot_error_comparison(results)
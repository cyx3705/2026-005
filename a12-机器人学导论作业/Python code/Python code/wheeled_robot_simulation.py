"""
Simulation reproduction of differential-drive path following experiments
from Balan et al. (ICTEST 2025).
"""

from __future__ import annotations

import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

from controllers import (
    MPCController,
    PurePursuitPIDController,
    RobotState,
    StanleyPIDController,
)


OUTPUT_DIR = Path(__file__).resolve().parent / "results"
DT = 0.05
MAX_STEPS = 2000
GOAL_TOLERANCE = 0.15


def make_path_1() -> np.ndarray:
    """Greenhouse-like gentle curve, similar to paper Path 1."""
    t = np.linspace(0.0, 1.0, 120)
    x = 8.0 * t
    y = 1.0 * np.sin(1.5 * np.pi * t)
    return np.column_stack([x, y])


def make_path_2() -> np.ndarray:
    """Moderate S-curve, similar to paper Path 2."""
    t = np.linspace(0.0, 1.0, 140)
    x = 7.0 * t
    y = 1.2 * np.sin(2.5 * np.pi * t)
    return np.column_stack([x, y])


def make_path_3() -> np.ndarray:
    """Sharper turns, similar to paper Path 3."""
    t = np.linspace(0.0, 1.0, 160)
    x = 8.0 * t
    y = 1.5 * np.sin(2.0 * np.pi * t) + 0.6 * np.sin(4.0 * np.pi * t)
    return np.column_stack([x, y])


def simulate(controller, path: np.ndarray, start: tuple[float, float, float]) -> tuple[np.ndarray, np.ndarray]:
    state = RobotState(*start)
    current_v = 0.0
    controller.reset()
    actual = [np.array([state.x, state.y])]
    errors = []

    last_idx = 0
    for _ in range(MAX_STEPS):
        distances = np.hypot(path[:, 0] - state.x, path[:, 1] - state.y)
        search_from = max(0, last_idx - 2)
        idx = int(np.argmin(distances[search_from:])) + search_from
        last_idx = max(last_idx, idx)
        errors.append(
            math.hypot(path[idx, 0] - state.x, path[idx, 1] - state.y)
        )
        goal = path[-1]
        if math.hypot(goal[0] - state.x, goal[1] - state.y) < GOAL_TOLERANCE:
            break

        control = controller.compute(state, path, current_v)
        current_v = control.v
        state.theta += control.omega * DT
        state.x += control.v * math.cos(state.theta) * DT
        state.y += control.v * math.sin(state.theta) * DT
        actual.append(np.array([state.x, state.y]))

    return np.asarray(actual), np.asarray(errors)


def error_metrics(errors: np.ndarray) -> dict[str, float]:
    return {
        "Emax": float(np.max(errors)),
        "Emin": float(np.min(errors)),
        "Eavg": float(np.mean(errors)),
        "Estd": float(np.std(errors)),
    }


def plot_path_following(
    path: np.ndarray,
    actual: np.ndarray,
    title: str,
    save_name: str,
) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    plt.figure(figsize=(8, 5))
    plt.plot(path[:, 0], path[:, 1], "c--", linewidth=2, label="Desired path")
    plt.plot(actual[:, 0], actual[:, 1], "k-", linewidth=1.5, label="Actual path")
    plt.plot(path[0, 0], path[0, 1], "go", markersize=10, label="Start")
    plt.plot(path[-1, 0], path[-1, 1], "rx", markersize=10, label="Goal")
    plt.xlabel("X (m)")
    plt.ylabel("Y (m)")
    plt.title(title)
    plt.grid(True, alpha=0.3)
    plt.legend()
    plt.axis("equal")
    plt.tight_layout()
    plt.savefig(OUTPUT_DIR / save_name, dpi=150)
    plt.close()


def run_all_experiments() -> list[dict]:
    paths = [make_path_1(), make_path_2(), make_path_3()]
    controllers = {
        "Pure Pursuit + PID": PurePursuitPIDController(),
        "Stanley + PID": StanleyPIDController(),
        "MPC": MPCController(),
    }
    results = []

    for path_id, path in enumerate(paths, start=1):
        start_theta = math.atan2(path[1, 1] - path[0, 1], path[1, 0] - path[0, 0])
        start = (path[0, 0], path[0, 1], start_theta)
        for name, controller in controllers.items():
            actual, errors = simulate(controller, path, start)
            metrics = error_metrics(errors)
            results.append(
                {
                    "path": path_id,
                    "controller": name,
                    **metrics,
                }
            )
            safe_name = name.lower().replace(" ", "_").replace("+", "plus")
            plot_path_following(
                path,
                actual,
                f"Path {path_id}: {name}",
                f"path{path_id}_{safe_name}.png",
            )
            print(
                f"Path {path_id} | {name:20s} | "
                f"Emax={metrics['Emax']:.3f} Emin={metrics['Emin']:.3f} "
                f"Eavg={metrics['Eavg']:.3f} Estd={metrics['Estd']:.3f}"
            )
    return results


def plot_error_comparison(results: list[dict]) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    controllers = ["Pure Pursuit + PID", "Stanley + PID", "MPC"]
    paths = [1, 2, 3]
    x = np.arange(len(paths))
    width = 0.25

    plt.figure(figsize=(9, 5))
    for i, controller in enumerate(controllers):
        avgs = [
            next(r["Eavg"] for r in results if r["path"] == p and r["controller"] == controller)
            for p in paths
        ]
        plt.bar(x + i * width, avgs, width, label=controller)
    plt.xlabel("Path ID")
    plt.ylabel("Average tracking error (m)")
    plt.title("Controller comparison (reproduction)")
    plt.xticks(x + width, [f"Path {p}" for p in paths])
    plt.legend()
    plt.grid(True, axis="y", alpha=0.3)
    plt.tight_layout()
    plt.savefig(OUTPUT_DIR / "error_comparison.png", dpi=150)
    plt.close()


if __name__ == "__main__":
    print("Running wheeled robot path-following reproduction...")
    experiment_results = run_all_experiments()
    plot_error_comparison(experiment_results)
    print(f"Figures saved to: {OUTPUT_DIR}")
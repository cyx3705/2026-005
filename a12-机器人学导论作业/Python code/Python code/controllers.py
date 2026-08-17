"""
Path-following controllers for differential-drive robots.
Reproduces the control strategies from:
  Balan et al., "A Holistic Approach to Wheeled Robot Design:
  Mechanical Structure and Control Strategies", ICTEST 2025.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

import numpy as np
from scipy.optimize import minimize


@dataclass
class RobotState:
    x: float
    y: float
    theta: float


@dataclass
class ControlOutput:
    v: float
    omega: float


class PIDController:
    """Discrete PID controller for linear velocity regulation."""

    def __init__(self, kp: float, ki: float, kd: float, dt: float):
        self.kp = kp
        self.ki = ki
        self.kd = kd
        self.dt = dt
        self.integral = 0.0
        self.prev_error = 0.0

    def reset(self) -> None:
        self.integral = 0.0
        self.prev_error = 0.0

    def compute(self, desired: float, current: float) -> float:
        error = desired - current
        self.integral += error * self.dt
        derivative = (error - self.prev_error) / self.dt
        self.prev_error = error
        return self.kp * error + self.ki * self.integral + self.kd * derivative


def normalize_angle(angle: float) -> float:
    return (angle + math.pi) % (2 * math.pi) - math.pi


def nearest_path_index(state: RobotState, path: np.ndarray) -> int:
    distances = np.hypot(path[:, 0] - state.x, path[:, 1] - state.y)
    return int(np.argmin(distances))


def lookahead_point(state: RobotState, path: np.ndarray, lookahead_dist: float) -> np.ndarray:
    idx = nearest_path_index(state, path)
    for i in range(idx, len(path)):
        dist = math.hypot(path[i, 0] - state.x, path[i, 1] - state.y)
        if dist >= lookahead_dist:
            return path[i]
    return path[-1]


def cross_track_error(state: RobotState, path: np.ndarray, idx: int) -> float:
    if idx >= len(path) - 1:
        idx = len(path) - 2
    p1 = path[idx]
    p2 = path[idx + 1]
    dx = p2[0] - p1[0]
    dy = p2[1] - p1[1]
    if dx == 0.0 and dy == 0.0:
        return math.hypot(state.x - p1[0], state.y - p1[1])
    t = ((state.x - p1[0]) * dx + (state.y - p1[1]) * dy) / (dx * dx + dy * dy)
    t = np.clip(t, 0.0, 1.0)
    proj_x = p1[0] + t * dx
    proj_y = p1[1] + t * dy
    error = math.hypot(state.x - proj_x, state.y - proj_y)
    cross = dx * (state.y - p1[1]) - dy * (state.x - p1[0])
    return error if cross >= 0 else -error


def path_heading(path: np.ndarray, idx: int) -> float:
    if idx >= len(path) - 1:
        idx = len(path) - 2
    return math.atan2(path[idx + 1, 1] - path[idx, 1], path[idx + 1, 0] - path[idx, 0])


class PurePursuitPIDController:
    """Pure Pursuit steering with PID linear velocity control."""

    def __init__(
        self,
        lookahead_dist: float = 0.8,
        desired_speed: float = 0.3,
        pid_gains: tuple[float, float, float] = (1.0, 0.03, 0.08),
        dt: float = 0.05,
    ):
        self.lookahead_dist = lookahead_dist
        self.desired_speed = desired_speed
        self.pid = PIDController(*pid_gains, dt)

    def reset(self) -> None:
        self.pid.reset()

    def compute(self, state: RobotState, path: np.ndarray, current_v: float) -> ControlOutput:
        target = lookahead_point(state, path, self.lookahead_dist)
        alpha = normalize_angle(
            math.atan2(target[1] - state.y, target[0] - state.x) - state.theta
        )
        omega = 2.0 * self.desired_speed * math.sin(alpha) / self.lookahead_dist
        v = self.pid.compute(self.desired_speed, current_v)
        v = float(np.clip(v, 0.0, self.desired_speed))
        return ControlOutput(v=v, omega=omega)


class StanleyPIDController:
    """Stanley lateral controller with PID linear velocity control."""

    def __init__(
        self,
        k_cte: float = 0.6,
        desired_speed: float = 0.3,
        wheelbase: float = 0.35,
        pid_gains: tuple[float, float, float] = (1.0, 0.03, 0.08),
        dt: float = 0.05,
    ):
        self.k_cte = k_cte
        self.desired_speed = desired_speed
        self.wheelbase = wheelbase
        self.pid = PIDController(*pid_gains, dt)
        self._last_idx = 0

    def reset(self) -> None:
        self.pid.reset()
        self._last_idx = 0

    def _progress_index(self, state: RobotState, path: np.ndarray) -> int:
        distances = np.hypot(path[:, 0] - state.x, path[:, 1] - state.y)
        search_from = max(0, self._last_idx - 2)
        local_idx = int(np.argmin(distances[search_from:])) + search_from
        self._last_idx = max(self._last_idx, local_idx)
        return min(self._last_idx, len(path) - 2)

    def compute(self, state: RobotState, path: np.ndarray, current_v: float) -> ControlOutput:
        idx = self._progress_index(state, path)
        e_ct = cross_track_error(state, path, idx)
        heading_ref = path_heading(path, idx)
        heading_error = normalize_angle(heading_ref - state.theta)
        v_cmd = self.pid.compute(self.desired_speed, current_v)
        v_cmd = float(np.clip(v_cmd, 0.08, self.desired_speed))
        v_cmd *= max(0.45, 1.0 - 0.6 * min(abs(e_ct), 1.2))
        # Paper Eq.(13): steering angle from heading and cross-track errors.
        delta = heading_error + math.atan2(self.k_cte * e_ct, v_cmd + 0.3)
        delta = float(np.clip(delta, -0.45, 0.45))
        omega = v_cmd * math.tan(delta) / self.wheelbase
        omega = float(np.clip(omega, -0.8, 0.8))
        return ControlOutput(v=v_cmd, omega=omega)


class MPCController:
    """Receding-horizon MPC based on unicycle kinematics."""

    def __init__(
        self,
        horizon: int = 10,
        dt: float = 0.1,
        desired_speed: float = 0.4,
        q_x: float = 10.0,
        q_y: float = 10.0,
        r_v: float = 0.1,
        r_w: float = 0.1,
        v_max: float = 0.5,
        omega_max: float = 1.2,
    ):
        self.horizon = horizon
        self.dt = dt
        self.desired_speed = desired_speed
        self.q_x = q_x
        self.q_y = q_y
        self.r_v = r_v
        self.r_w = r_w
        self.v_max = v_max
        self.omega_max = omega_max

    def reset(self) -> None:
        pass

    def _rollout(self, state: RobotState, controls: np.ndarray) -> np.ndarray:
        traj = np.zeros((self.horizon + 1, 3))
        traj[0] = [state.x, state.y, state.theta]
        for k in range(self.horizon):
            v, omega = controls[2 * k], controls[2 * k + 1]
            x, y, theta = traj[k]
            traj[k + 1, 0] = x + v * math.cos(theta) * self.dt
            traj[k + 1, 1] = y + v * math.sin(theta) * self.dt
            traj[k + 1, 2] = theta + omega * self.dt
        return traj

    def _reference_window(self, state: RobotState, path: np.ndarray) -> np.ndarray:
        idx = nearest_path_index(state, path)
        refs = []
        for k in range(self.horizon + 1):
            ref_idx = min(idx + k, len(path) - 1)
            refs.append(path[ref_idx])
        return np.asarray(refs)

    def _cost(self, controls: np.ndarray, state: RobotState, refs: np.ndarray) -> float:
        traj = self._rollout(state, controls)
        cost = 0.0
        for k in range(1, self.horizon + 1):
            dx = traj[k, 0] - refs[k, 0]
            dy = traj[k, 1] - refs[k, 1]
            cost += self.q_x * dx * dx + self.q_y * dy * dy
        for k in range(self.horizon):
            v, omega = controls[2 * k], controls[2 * k + 1]
            cost += self.r_v * v * v + self.r_w * omega * omega
        return cost

    def compute(self, state: RobotState, path: np.ndarray, current_v: float) -> ControlOutput:
        refs = self._reference_window(state, path)
        n_vars = 2 * self.horizon
        x0 = np.zeros(n_vars)
        x0[0::2] = self.desired_speed
        bounds = [(0.0, self.v_max), (-self.omega_max, self.omega_max)] * self.horizon
        result = minimize(
            self._cost,
            x0,
            args=(state, refs),
            method="SLSQP",
            bounds=bounds,
            options={"maxiter": 80, "ftol": 1e-4},
        )
        if result.success:
            v, omega = result.x[0], result.x[1]
        else:
            v, omega = self.desired_speed, 0.0
        return ControlOutput(v=float(v), omega=float(omega))
%% 论文复现: A Holistic Approach to Wheeled Robot Design
% 差速驱动农业机器人路径跟踪仿真
% 对比 Pure Pursuit+PID / Stanley+PID / MPC 三种控制器
% Balan et al., ICTEST 2025
clear; clc; close all;

%% 仿真参数
dt = 0.05;
max_steps = 2000;
goal_tol = 0.15;
desired_speed = 0.3;
lookahead_dist = 0.8;
wheelbase = 0.35;
k_cte = 0.6;
output_dir = fullfile(fileparts(mfilename('fullpath')), 'matlab_results');
if ~exist(output_dir, 'dir')
    mkdir(output_dir);
end

%% 三条参考路径 (对应论文 Path 1/2/3)
paths = {
    make_path_1();
    make_path_2();
    make_path_3()
};

controller_names = {'Pure Pursuit + PID', 'Stanley + PID', 'MPC'};
results = [];

for path_id = 1:numel(paths)
    ref_path = paths{path_id};
    start_theta = atan2(ref_path(2,2) - ref_path(1,2), ref_path(2,1) - ref_path(1,1));
    start_state = [ref_path(1,1), ref_path(1,2), start_theta];

    controllers = {
        @(s,p,v) pure_pursuit_pid(s,p,v,desired_speed,lookahead_dist,dt);
        @(s,p,v) stanley_pid(s,p,v,desired_speed,wheelbase,k_cte,dt);
        @(s,p,v) mpc_controller(s,p,v,desired_speed,dt)
    };

    for c_id = 1:numel(controllers)
        [actual, errors] = simulate_robot(controllers{c_id}, ref_path, start_state, dt, max_steps, goal_tol);
        metrics = error_metrics(errors);
        results = [results; path_id, c_id, metrics.Emax, metrics.Emin, metrics.Eavg, metrics.Estd]; %#ok<AGROW>

        fig = figure('Color','w','Visible','off');
        plot(ref_path(:,1), ref_path(:,2), 'c--', 'LineWidth', 2); hold on;
        plot(actual(:,1), actual(:,2), 'k-', 'LineWidth', 1.5);
        plot(ref_path(1,1), ref_path(1,2), 'go', 'MarkerSize', 8, 'MarkerFaceColor', 'g');
        plot(ref_path(end,1), ref_path(end,2), 'rx', 'MarkerSize', 10, 'LineWidth', 2);
        axis equal; grid on;
        xlabel('X (m)'); ylabel('Y (m)');
        title(sprintf('Path %d: %s', path_id, controller_names{c_id}));
        legend('Desired path', 'Actual path', 'Start', 'Goal', 'Location', 'best');
        saveas(fig, fullfile(output_dir, sprintf('path%d_ctrl%d.png', path_id, c_id)));
        close(fig);

        fprintf('Path %d | %-20s | Emax=%.3f Emin=%.3f Eavg=%.3f Estd=%.3f\n', ...
            path_id, controller_names{c_id}, metrics.Emax, metrics.Emin, metrics.Eavg, metrics.Estd);
    end
end

%% 汇总表格
T = array2table(results, 'VariableNames', {'Path','ControllerID','Emax','Emin','Eavg','Estd'});
T.Controller = controller_names(T.ControllerID)';
disp(T);
writetable(T, fullfile(output_dir, 'tracking_errors.csv'));

%% 平均误差柱状图
figure('Color','w');
bar_data = zeros(3,3);
for i = 1:3
    for j = 1:3
        idx = (i-1)*3 + j;
        bar_data(i,j) = results(idx,5);
    end
end
bar(bar_data);
set(gca, 'XTickLabel', {'Path 1','Path 2','Path 3'});
ylabel('Average tracking error (m)');
title('Controller comparison (MATLAB reproduction)');
legend(controller_names, 'Location', 'northwest');
grid on;
saveas(gcf, fullfile(output_dir, 'error_comparison.png'));

fprintf('\nResults saved to: %s\n', output_dir);

%% ===================== 局部函数 =====================

function path = make_path_1()
    t = linspace(0, 1, 120)';
    path = [8*t, 1.0*sin(1.5*pi*t)];
end

function path = make_path_2()
    t = linspace(0, 1, 140)';
    path = [7*t, 1.2*sin(2.5*pi*t)];
end

function path = make_path_3()
    t = linspace(0, 1, 160)';
    path = [8*t, 1.5*sin(2*pi*t) + 0.6*sin(4*pi*t)];
end

function [actual, errors] = simulate_robot(controller, ref_path, start_state, dt, max_steps, goal_tol)
    state = start_state;
    current_v = 0;
    actual = state(1:2);
    errors = [];

    for step = 1:max_steps
        [~, idx] = min(hypot(ref_path(:,1) - state(1), ref_path(:,2) - state(2)));
        errors(end+1) = hypot(ref_path(idx,1) - state(1), ref_path(idx,2) - state(2)); %#ok<AGROW>
        if hypot(ref_path(end,1) - state(1), ref_path(end,2) - state(2)) < goal_tol
            break;
        end
        ctrl = controller(state, ref_path, current_v);
        current_v = ctrl.v;
        state(3) = state(3) + ctrl.omega * dt;
        state(1) = state(1) + ctrl.v * cos(state(3)) * dt;
        state(2) = state(2) + ctrl.v * sin(state(3)) * dt;
        actual = [actual; state(1:2)]; %#ok<AGROW>
    end
    errors = errors(:);
end

function metrics = error_metrics(errors)
    metrics.Emax = max(errors);
    metrics.Emin = min(errors);
    metrics.Eavg = mean(errors);
    metrics.Estd = std(errors);
end

function ctrl = pure_pursuit_pid(state, path, current_v, desired_speed, lookahead_dist, dt)
    persistent pid_state
    if isempty(pid_state), pid_state = struct('integ',0,'prev',0); end
    [target, ~] = lookahead_point(state, path, lookahead_dist);
    alpha = wrap_to_pi(atan2(target(2)-state(2), target(1)-state(1)) - state(3));
    omega = 2 * desired_speed * sin(alpha) / lookahead_dist;
    v = pid_velocity(desired_speed, current_v, pid_state, dt, [1.2, 0.05, 0.1]);
    ctrl.v = max(0, min(v, desired_speed));
    ctrl.omega = omega;
end

function ctrl = stanley_pid(state, path, current_v, desired_speed, wheelbase, k_cte, dt)
    persistent pid_state
    if isempty(pid_state), pid_state = struct('integ',0,'prev',0); end
    [~, idx] = min(hypot(path(:,1) - state(1), path(:,2) - state(2)));
    e_ct = cross_track_error(state, path, idx);
    heading_ref = path_heading(path, idx);
    heading_error = wrap_to_pi(heading_ref - state(3));
    v = pid_velocity(desired_speed, current_v, pid_state, dt, [1.0, 0.03, 0.08]);
    v = max(0.08, min(v, desired_speed));
    v = v * max(0.45, 1.0 - 0.6 * min(abs(e_ct), 1.2));
    soft_v = v + 0.3;
    delta = heading_error + atan2(k_cte * e_ct, soft_v);
    delta = max(-0.45, min(0.45, delta));
    omega = v * tan(delta) / wheelbase;
    omega = max(-0.8, min(0.8, omega));
    ctrl.v = v;
    ctrl.omega = omega;
end

function ctrl = mpc_controller(state, path, ~, desired_speed, dt)
    horizon = 10;
    [~, idx] = min(hypot(path(:,1) - state(1), path(:,2) - state(2)));
    refs = zeros(horizon+1, 2);
    for k = 0:horizon
        ref_idx = min(idx + k, size(path,1));
        refs(k+1,:) = path(ref_idx,:);
    end
    u0 = repmat([desired_speed, 0], horizon, 1);
    lb = repmat([0, -1.2], horizon, 1);
    ub = repmat([0.5, 1.2], horizon, 1);
    cost_fun = @(u) mpc_cost(reshape(u,2,[])', state, refs, dt);
    u_opt = fmincon(cost_fun, u0(:), [], [], [], [], lb(:), ub(:), [], optimset('Display','off'));
    u_mat = reshape(u_opt, 2, [])';
    ctrl.v = u_mat(1,1);
    ctrl.omega = u_mat(1,2);
end

function cost = mpc_cost(controls, state, refs, dt)
    horizon = size(controls,1);
    traj = zeros(horizon+1, 3);
    traj(1,:) = state;
    for k = 1:horizon
        v = controls(k,1); w = controls(k,2);
        traj(k+1,1) = traj(k,1) + v * cos(traj(k,3)) * dt;
        traj(k+1,2) = traj(k,2) + v * sin(traj(k,3)) * dt;
        traj(k+1,3) = traj(k,3) + w * dt;
    end
    cost = 0;
    for k = 2:horizon+1
        dx = traj(k,1) - refs(k,1);
        dy = traj(k,2) - refs(k,2);
        cost = cost + 10*(dx^2 + dy^2);
    end
    cost = cost + 0.1*sum(controls(:,1).^2 + controls(:,2).^2);
end

function v = pid_velocity(desired, current, pid_state, dt, gains)
    kp = gains(1); ki = gains(2); kd = gains(3);
    e = desired - current;
    pid_state.integ = pid_state.integ + e * dt;
    d = (e - pid_state.prev) / dt;
    pid_state.prev = e;
    v = kp*e + ki*pid_state.integ + kd*d;
end

function [target, idx] = lookahead_point(state, path, lookahead_dist)
    [~, idx] = min(hypot(path(:,1) - state(1), path(:,2) - state(2)));
    target = path(end,:);
    for i = idx:size(path,1)
        if hypot(path(i,1)-state(1), path(i,2)-state(2)) >= lookahead_dist
            target = path(i,:);
            return;
        end
    end
end

function e_ct = cross_track_error(state, path, idx)
    if idx >= size(path,1), idx = size(path,1)-1; end
    p1 = path(idx,:); p2 = path(idx+1,:);
    dx = p2(1)-p1(1); dy = p2(2)-p1(2);
    if dx == 0 && dy == 0
        e_ct = hypot(state(1)-p1(1), state(2)-p1(2));
        return;
    end
    t = ((state(1)-p1(1))*dx + (state(2)-p1(2))*dy) / (dx^2 + dy^2);
    t = min(max(t,0),1);
    proj = [p1(1)+t*dx, p1(2)+t*dy];
    err = hypot(state(1)-proj(1), state(2)-proj(2));
    cross = dx*(state(2)-p1(2)) - dy*(state(1)-p1(1));
    if cross < 0, e_ct = -err; else, e_ct = err; end
end

function heading = path_heading(path, idx)
    if idx >= size(path,1), idx = size(path,1)-1; end
    heading = atan2(path(idx+1,2)-path(idx,2), path(idx+1,1)-path(idx,1));
end

function a = wrap_to_pi(a)
    a = mod(a + pi, 2*pi) - pi;
end
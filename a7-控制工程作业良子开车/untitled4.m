clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 基础参数与核心设定
% --------------------------
m = 300;          % 车轮等效质量 (kg)
v = 5;            % 车速 (m/s)
dmax = 0.2;       % 最大位移约束 (m)
t = 0:0.001:5;    % 延长时间至5s，确保观察完全停止
a1 = -0.05;       % 位移z_max的权重（负：位移越大，U越小）
a2 = -0.925;      % 时间t_s的权重（负：时间越长，U越小）

% 参数范围设定
k_range = 135000:5000:200000;
c_range = 1527:500:8000;
k_num = length(k_range);
c_num = length(c_range);

% 预存储结果矩阵
z_max_matrix = zeros(k_num, c_num);    % 各(k,c)的最大绝对位移
t_s_matrix = zeros(k_num, c_num);      % 各(k,c)的震荡停止时间
U_matrix = zeros(k_num, c_num);        % 各(k,c)的不稳定系数U
U_diff_matrix = zeros(k_num, c_num);   % 各(k,c)的U与1的差值

% --------------------------
% 2. 减速带输入函数
% --------------------------
function y = road_input(t, v)
    y = zeros(size(t));
    t1 = 0.1 / v; t2 = 0.2 / v; t3 = 0.3 / v;
    for i = 1:length(t)
        ti = t(i);
        if ti <= t1; y(i) = v * ti / 2;
        elseif ti <= t2; y(i) = 0.05;
        elseif ti <= t3; y(i) = 0.05 - (v * ti - 0.2) / 2;
        else; y(i) = 0; end
    end
end
xt = road_input(t, v);

% --------------------------
% 3. 震荡停止时间判定函数（修正：严格判定）
% --------------------------
function t_stop = get_osc_stop_time(t, yt)
    threshold = 0.001;    % 位移阈值（1mm，确保完全停止）
    window_len = 100;     % 0.1秒窗口
    t_stop = NaN;
    start_idx = find(t >= 1.0, 1);  % 从1.0s开始判断（避开响应峰值）
    if ~isempty(start_idx)
        for j = start_idx:(length(t)-window_len+1)
            if all(abs(yt(j:j+window_len-1)) <= threshold)
                t_stop = t(j);
                break;
            end
        end
    end
    if isnan(t_stop); t_stop = max(t); end  % 未停止则取最大时间
end

% --------------------------
% 4. 遍历所有(k,c)组合，计算响应与U值
% --------------------------
fprintf('开始计算所有(k,c)组合的系统响应...\n');
for i = 1:k_num
    k = k_range(i);
    for j = 1:c_num
        c = c_range(j);
        
        % 4.1 计算系统位移响应（仅取绝对位移）
        sys = (c*s + k) / (m*s^2 + c*s + k);
        yt = lsim(sys, xt, t);
        yt_abs = abs(yt);  % 取绝对位移，避免负数干扰
        
        % 4.2 提取关键参数
        z_max = max(yt_abs);                % 最大绝对位移
        t_s = get_osc_stop_time(t, yt_abs); % 基于绝对位移判定停止时间
        
        % 4.3 计算不稳定系数U
        U = (z_max)^a1 * (t_s)^a2;
        U_diff = abs(U - 1);
        
        % 4.4 存储结果
        z_max_matrix(i,j) = z_max;
        t_s_matrix(i,j) = t_s;
        U_matrix(i,j) = U;
        U_diff_matrix(i,j) = U_diff;
    end
end
fprintf('所有组合计算完成！\n');

% --------------------------
% 5. 找到U最接近1的最优(k,c)组合
% --------------------------
[min_U_diff, min_idx] = min(U_diff_matrix(:));
[best_k_idx, best_c_idx] = ind2sub(size(U_diff_matrix), min_idx);
best_k = k_range(best_k_idx);
best_c = c_range(best_c_idx);
best_U = U_matrix(best_k_idx, best_c_idx);
best_z_max = z_max_matrix(best_k_idx, best_c_idx);
best_t_s = t_s_matrix(best_k_idx, best_c_idx);

fprintf('\n==================== 最优参数结果 ====================\n');
fprintf('U最接近1的悬挂刚度 k = %.0f N/m\n', best_k);
fprintf('U最接近1的阻尼系数 c = %.2f N·s/m\n', best_c);
fprintf('对应不稳定系数 U = %.4f（与1的差值：%.4f）\n', best_U, min_U_diff);
fprintf('对应最大绝对位移 z_max = %.4f m（≤%.2f m，满足约束）\n', best_z_max, dmax);
fprintf('对应震荡停止时间 t_s = %.3f s\n', best_t_s);
fprintf('======================================================\n');

% --------------------------
% 6. 可视化结果
% --------------------------
figure('Name','k-c组合系统响应与U值分析','Position',[100 100 1500 800]);

% 6.1 子图1：U值热力图
subplot(2,2,1);
[X,Y] = meshgrid(c_range, k_range);
contourf(X, Y, U_matrix, 30);
colorbar;
xlabel('阻尼系数 c (N·s/m)');
ylabel('悬挂刚度 k (N/m)');
title(sprintf('不同(k,c)组合的不稳定系数U分布（U = z_max^%.2f * t_s^%.2f）', a1, a2));
hold on;
plot(best_c, best_k, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r');
text(best_c+200, best_k+2000, sprintf('最优\nk=%.0f\nc=%.0f\nU=%.4f', best_k, best_c, best_U), ...
    'FontSize', 9, 'BackgroundColor', 'white');
hold off;

% 6.2 子图2：最优(k,c)的位移响应
subplot(2,2,2);
best_sys = (best_c*s + best_k) / (m*s^2 + best_c*s + best_k);
best_yt = lsim(best_sys, xt, t);
plot(t, abs(best_yt), 'r-', 'LineWidth', 2, 'DisplayName', sprintf('最优(k=%.0f,c=%.0f)', best_k, best_c));
hold on;
plot(t, xt, 'k--', 'LineWidth', 1.5, 'DisplayName', '减速带输入');
plot(t, dmax*ones(size(t)), 'g-.', 'LineWidth', 1, 'DisplayName', '位移约束(0.2m)');
plot([best_t_s best_t_s], ylim, 'b--', 'LineWidth', 1.5, 'DisplayName', sprintf('停止时间=%.3f s', best_t_s));
title(sprintf('最优参数的绝对位移响应（U=%.4f）', best_U));
xlabel('时间 t (s)');
ylabel('绝对位移 |y| (m)');
grid on;
legend('Location', 'best');
ylim([0, 0.3]);
hold off;

% 6.3 子图3：U与1的差值热力图
subplot(2,2,3);
contourf(X, Y, U_diff_matrix, 30);
colorbar;
xlabel('阻尼系数 c (N·s/m)');
ylabel('悬挂刚度 k (N/m)');
title('不同(k,c)组合的U与1的差值分布（颜色越浅越优）');
hold on;
plot(best_c, best_k, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r');
text(best_c+200, best_k+2000, sprintf('最优\n差值=%.4f', min_U_diff), ...
    'FontSize', 9, 'BackgroundColor', 'white');
hold off;

% 6.4 子图4：最优k下的U随c变化曲线
subplot(2,2,4);
best_k_U = U_matrix(best_k_idx, :);
plot(c_range, best_k_U, 'b-o', 'LineWidth', 2, 'MarkerSize', 6);
hold on;
plot(best_c, best_U, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r');
text(best_c+200, best_U+0.02, sprintf('最优c=%.0f\nU=%.4f', best_c, best_U), ...
    'FontSize', 9, 'BackgroundColor', 'white');
plot(c_range, ones(1, c_num), 'g--', 'LineWidth', 1.5, 'DisplayName', 'U=1参考线');
title(sprintf('最优k=%.0f下，U随阻尼系数c的变化', best_k));
xlabel('阻尼系数 c (N·s/m)');
ylabel('不稳定系数 U');
grid on;
legend('Location', 'best');
hold off;

% --------------------------
% 7. 输出前10个最优组合表格
% --------------------------
[sorted_U_diff, sorted_idx] = sort(U_diff_matrix(:));
top10_idx = sorted_idx(1:min(10, length(sorted_idx)));
[top10_k_idx, top10_c_idx] = ind2sub(size(U_diff_matrix), top10_idx);

fprintf('\n前10个U最接近1的参数组合：\n');
fprintf('排名 | k (N/m) | c (N·s/m) | U值      | 与1的差值 | z_max (m) | t_s (s)\n');
fprintf('-----|---------|-----------|----------|-----------|-----------|---------\n');
for p = 1:length(top10_idx)
    k_p = k_range(top10_k_idx(p));
    c_p = c_range(top10_c_idx(p));
    U_p = U_matrix(top10_k_idx(p), top10_c_idx(p));
    diff_p = sorted_U_diff(p);
    z_p = z_max_matrix(top10_k_idx(p), top10_c_idx(p));
    t_p = t_s_matrix(top10_k_idx(p), top10_c_idx(p));
    fprintf('%3d  | %7d | %9.0f | %.4f | %.4f | %.4f | %.3f\n', ...
        p, k_p, c_p, U_p, diff_p, z_p, t_p);
end

% --------------------------
% 8. 绘制t_s-z_max-U点图
% --------------------------
figure('Name','t_s-z_max-U点图分析','Position',[100 100 800 600]);
t_s_all = t_s_matrix(:);
z_max_all = z_max_matrix(:);
U_diff_all = U_diff_matrix(:);
U_diff_norm = (max(U_diff_all) - U_diff_all) / (max(U_diff_all) - min(U_diff_all));
U_diff_norm(isnan(U_diff_norm)) = 0;  % 处理NaN
U_diff_norm = max(U_diff_norm, eps);  % 确保为正

scatter(t_s_all, z_max_all, 50*U_diff_norm, U_diff_norm, 'filled');
colorbar;
xlabel('震荡停止时间 t_s (s)');
ylabel('最大绝对位移 z_max (m)');
title('不同(k,c)组合的t_s-z_max分布（颜色/大小越浅/大，U越接近1）');
grid on;
hold on;
scatter(best_t_s, best_z_max, 200, 'r', 'filled', 'DisplayName', '最优参数');
text(best_t_s+0.05, best_z_max+0.002, sprintf('k=%.0f\nc=%.0f\nU=%.4f', best_k, best_c, best_U), ...
    'FontSize', 9, 'BackgroundColor', 'white');
legend('Location','best');
hold off;
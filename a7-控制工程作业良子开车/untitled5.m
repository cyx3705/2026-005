clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 基础参数与核心设定
% --------------------------
m = 300;          % 车轮等效质量 (kg)
v = 5;            % 车速 (m/s)
dmax = 0.2;       % 最大位移约束 (m)
t = 0:0.001:5;    % 时间向量
a1 = 1;           % 位移权重（加法逻辑，a1=1）
a2 = 1;           % 时间权重（加法逻辑，a2=1）

% 参数范围设定
k_range = 20000:5000:120000;
c_range = 3135:500:8000;
k_num = length(k_range);
c_num = length(c_range);

% 预存储结果矩阵
z_max_matrix = zeros(k_num, c_num);    % 各(k,c)的最大位移（含正负）
t_s_matrix = zeros(k_num, c_num);      % 各(k,c)的震荡停止时间
U_matrix = zeros(k_num, c_num);        % 各(k,c)的不稳定系数U（U = z_max + t_s）
U_diff_matrix = zeros(k_num, c_num);   % 各(k,c)的U与最小值的差值

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
% 3. 震荡停止时间判定函数
% --------------------------
function t_stop = get_osc_stop_time(t, yt)
    threshold = 0.001;    % 位移阈值
    window_len = 100;     % 0.1秒窗口
    t_stop = NaN;
    start_idx = find(t >=0, 1);  
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
fprintf('计算所有(k,c)组合的系统响应...\n');
for i = 1:k_num
    k = k_range(i);
    for j = 1:c_num
        c = c_range(j);
        
        % 4.1 计算系统位移响应（保留正负）
        sys = (c*s + k) / (m*s^2 + c*s + k);
        yt = lsim(sys, xt, t);
        
        % 4.2 提取关键参数
        z_max = max(abs(yt));                % 最大位移绝对值（用于U计算）
        t_s = get_osc_stop_time(t, yt);      % 震荡停止时间
        U = (z_max)*(t_s);                     % 乘法逻辑的U值
        U_diff = U;  % 此处直接用U作为差值（因目标是最小化U）
        
        % 4.3 存储结果
        z_max_matrix(i,j) = z_max;
        t_s_matrix(i,j) = t_s;
        U_matrix(i,j) = U;
        U_diff_matrix(i,j) = U_diff;
    end
end

% --------------------------
% 5. 找到U最小的最优(k,c)组合
% --------------------------
[min_U, min_idx] = min(U_matrix(:));
[best_k_idx, best_c_idx] = ind2sub(size(U_matrix), min_idx);
best_k = k_range(best_k_idx);
best_c = c_range(best_c_idx);
best_z_max = z_max_matrix(best_k_idx, best_c_idx);
best_t_s = t_s_matrix(best_k_idx, best_c_idx);

fprintf('\n最优参数结果（U = z_max + t_s 最小化）：\n');
fprintf('k = %.0f N/m, c = %.2f N·s/m\n', best_k, best_c);
fprintf('U = %.4f, 最大位移 = %.4f m, 停止时间 = %.3f s\n', min_U, best_z_max, best_t_s);

% --------------------------
% 6. 可视化结果
% --------------------------
figure('Name','U最小化分析（a1=1,a2=1）','Position',[100 100 1500 800]);

% 6.1 子图1：U值热力图
subplot(2,2,1);
[X,Y] = meshgrid(c_range, k_range);
contourf(X, Y, U_matrix, 30);
colorbar;
xlabel('阻尼系数 c (N·s/m)');
ylabel('悬挂刚度 k (N/m)');
title('U = z_max + t_s 分布（颜色越浅U越小）');
hold on;
plot(best_c, best_k, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r');
text(best_c+200, best_k+2000, sprintf('最优\nk=%.0f\nc=%.0f\nU=%.4f', best_k, best_c, min_U), ...
    'FontSize', 9, 'BackgroundColor', 'white');
hold off;

% 6.2 子图2：最优(k,c)的位移响应（保留正负）
subplot(2,2,2);
best_sys = (best_c*s + best_k) / (m*s^2 + best_c*s + best_k);
best_yt = lsim(best_sys, xt, t);
plot(t, best_yt, 'r-', 'LineWidth', 2, 'DisplayName', sprintf('最优(k=%.0f,c=%.0f)', best_k, best_c));
hold on;
plot(t, xt, 'k--', 'LineWidth', 1.5, 'DisplayName', '减速带输入');
plot(t, dmax*ones(size(t)), 'g-.', 'LineWidth', 1, 'DisplayName', '位移约束(0.2m)');
plot(t, -dmax*ones(size(t)), 'g-.', 'LineWidth', 1);
plot([best_t_s best_t_s], ylim, 'b--', 'LineWidth', 1.5, 'DisplayName', sprintf('停止时间=%.3f s', best_t_s));
title('最优参数的位移响应（保留正负）');
xlabel('时间 t (s)');
ylabel('位移 y (m)');
grid on;
legend('Location', 'best');
ylim([-0.3, 0.3]);
hold off;

% 6.3 子图3：U与最小值的差值热力图
subplot(2,2,3);
contourf(X, Y, U_diff_matrix, 30);
colorbar;
xlabel('阻尼系数 c (N·s/m)');
ylabel('悬挂刚度 k (N/m)');
title('U与最小值的差值分布');
hold on;
plot(best_c, best_k, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r');
hold off;

% 6.4 子图4：t_s-z_max-U点图（固定点大小，仅用颜色表示U值）
subplot(2,2,4);
t_s_all = t_s_matrix(:);
z_max_all = z_max_matrix(:);
U_all = U_matrix(:);

% 固定点大小为50，仅用颜色映射U值（颜色越浅U越小）
scatter(t_s_all, z_max_all, 50, U_all, 'filled');
colorbar;
caxis([min(U_all) max(U_all)]);  % 固定颜色范围，确保对比一致

xlabel('震荡停止时间 t_s (s)');
ylabel('最大绝对位移 z_max (m)');
title('t_s-z_max-U 分布（颜色越浅，U越小）');
grid on;
hold on;

% 突出显示最优参数点（红色大圆点）
scatter(best_t_s, best_z_max, 200, 'r', 'filled', 'DisplayName', '最优参数');
text(best_t_s+0.05, best_z_max+0.002, sprintf('k=%.0f\nc=%.0f\nU=%.4f', best_k, best_c, min_U), ...
    'FontSize', 9, 'BackgroundColor', 'white');

legend('Location','best');
hold off;
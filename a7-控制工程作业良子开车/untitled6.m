clc; clear; close all;
s = tf('s');

% 1. 核心参数（保留300kg单轮质量）
m = 300;          % 单轮分配的车身等效质量 (kg)
k_test = 20000;   % 单轮弹簧刚度 (N/m)
c_test = 4135;    % 单轮阻尼 (N·s/m)
v = 5;            % 车速 (m/s)
dmax = 0.2;       % 最大位移约束 (m)
t = 0:0.0001:2;    % 时间向量
dt = t(2)-t(1);

% 2. 减速带输入函数（不修改）
function y = road_input(t, v)
    y = zeros(size(t));
    t1 = 0.1 / v;   % 上升段结束时间（0.02s）
    t2 = 0.2 / v;   % 平台段结束时间（0.04s）
    t3 = 0.3 / v;   % 下降段结束时间（0.06s）
    for i = 1:length(t)
        ti = t(i);
        if ti <= t1
            y(i) = v * ti / 2;  % 上升段（线性增长）
        elseif ti > t1 && ti <= t2
            y(i) = 0.05;        % 平台段（0.05m）
        elseif ti > t2 && ti <= t3
            y(i) = 0.05 - (v * ti - 0.2) / 2;  % 下降段（线性回落）
        else
            y(i) = 0;           % 输入结束后为0
        end
    end
end
xt = road_input(t, v);  % 生成减速带输入曲线

% 3. 计算响应（方案1：高精度求导，避免非因果）
sys_test = (c_test*s + k_test) / (m*s^2 + c_test*s + k_test);
yt_test = lsim(sys_test, xt, t);  % 位移响应（无阶数问题）


% 高精度求导：先Savitzky-Golay滤波平滑，再二阶差分
%yt_smoothed = sgolayfilt(yt_test, 3, 15);  % 3次多项式拟合，15点窗口
a_test = gradient(gradient(yt_test, dt), dt);  % 求加速度

% 提取关键指标
max_yt = max(abs(yt_test));
max_a = max(abs(a_test));
fprintf('当前k=%d N/m，最大单轮位移=%.4f m，最大加速度=%.2f m/s²（%.2f g）\n', ...
    k_test, max_yt, max_a, max_a/9.8);

% 4. 绘制位移响应图（不修改）
figure('Name','单轮位移响应图','Position',[200 200 1000 600]);
hold on; grid on;
plot(t, yt_test, 'r-', 'LineWidth', 2.5, 'DisplayName', sprintf('单轮位移响应（k=%.0f, c=%.0f）', k_test, c_test));
plot(t, xt, 'k--', 'LineWidth', 1.8, 'DisplayName', '减速带输入位移');
plot(t, dmax*ones(size(t)), 'g-.', 'LineWidth', 1.5, 'DisplayName', sprintf('位移约束（±%.2f m）', dmax));
plot(t, -dmax*ones(size(t)), 'g-.', 'LineWidth', 1.5);
xlabel('时间 t (s)', 'FontSize', 12);
ylabel('位移 y (m)', 'FontSize', 12);
title('单轮位移响应', 'FontSize', 13);
legend('Location', 'best', 'FontSize', 10);
ylim([-0.3, 0.3]);  
xlim([0, 2]);       
hold off;

% 5. 绘制加速度响应图（不修改）
figure('Name','加速度响应图','Position',[200 400 1000 600]);
hold on; grid on;
plot(t, a_test, 'b-', 'LineWidth', 2.5, 'DisplayName', sprintf('加速度响应（k=%.0f, c=%.0f）', k_test, c_test));
plot(t, 9.8*ones(size(t)), 'r--', 'LineWidth', 1.5, 'DisplayName', '重力加速度 g');
plot(t, -9.8*ones(size(t)), 'r--', 'LineWidth', 1.5);
xlabel('时间 t (s)', 'FontSize', 12);
ylabel('加速度 a (m/s²)', 'FontSize', 12);
title('加速度响应', 'FontSize', 13);
legend('Location', 'best', 'FontSize', 10);
ylim([-25, 25]);  
xlim([0, 2]);     
hold off;
clc; clear; close all;

s = tf('s');
% 系统基础参数
P = 400;         % 良子与焖子总质量 (kg)
dmax = 0.2;      % 最大刚性位移约束 (m)
m = 300;         % 车轮等效质量 (kg)
c = 5790;         % 阻尼系数 (N·s/m)
k_base = 10000;  % 基础刚度 (N/m)

% 刚性条件约束：计算最小悬挂刚度
kmin = 10*m / dmax;  % 最小刚度 (N/m)
fprintf('最小刚度约束: kmin = %.2f N/m\n', kmin);

% 定义减速带输入函数（10km/h转换为m/s）
v = 5;  % 车速转换 (m/s)
% 分段函数实现（使用arrayfun确保对数组t的每个元素逐元素计算）
function y = road_input(t, v)
    % 初始化输出数组
    y = zeros(size(t));
    % 计算各阶段时间阈值
    t1 = 0.1 / v;   % 上升段结束时间
    t2 = 0.2 / v;   % 平台段结束时间
    t3 = 0.3 / v;   % 下降段结束时间
    
    % 逐元素判断并计算输入值
    for i = 1:length(t)
        ti = t(i);
        if ti <= t1
            % 上升段：线性递增至0.05m
            y(i) = v * ti / 2;
        elseif ti > t1 && ti <= t2
            % 平台段：保持0.05m
            y(i) = 0.05;
        elseif ti > t2 && ti <= t3
            % 下降段：线性递减至0
            y(i) = 0.05 - (v * ti - 0.2) / 2;
        else
            % 其他时间：输入为0
            y(i) = 0;
        end
    end
end

% 时间向量
t = 0:0.001:2;

% 生成输入位移曲线
xt = road_input(t,v);

% 定义k值范围
k_values = kmin:5000:200000;

% 初始化图形
figure('Name','不同刚度k对应的系统响应总值','Position',[100 100 1000 600]);
hold on; grid on;
xlabel('时间 t (s)');
ylabel('位移响应 (m)');
title('不同悬挂刚度k的系统位移响应对比');

% 循环计算不同k值的响应并绘图
for i = 1:length(k_values)
    k = k_values(i);
    SYS = (c*s + k) / (m*s^2 + c*s + k);
    yt = lsim(SYS, xt, t)+10*m/k;
    plot(t, yt, 'DisplayName', sprintf('k = %.0f N/m', k));
end

% 绘制输入位移曲线和约束线
plot(t, xt, 'k--', 'LineWidth', 1.5, 'DisplayName', '减速带输入');
plot(t, dmax*ones(size(t)), 'r-.', 'LineWidth', 1, 'DisplayName', '最大位移约束');
plot(t, -dmax*ones(size(t)), 'r-.', 'LineWidth', 1);

legend('Location', 'bestoutside');
ylim([-0.3 0.3]);
hold off;
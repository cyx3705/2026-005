clc; clear; close all;

s = tf('s');
% 系统基础参数
P = 400;         % 质量 (kg)
dmax = 0.2;      % 最大刚性位移约束 (m)
m = 300;         % 车轮等效质量 (kg)
c = 5790;        % 阻尼系数 (N·s/m)
k_base = 10000;  % 基础刚度 (N/m)

% 刚性条件约束：计算最小悬挂刚度
kmin = 10*m / dmax;  % 最小刚度 (N/m)
fprintf('最小刚度约束: kmin = %.2f N/m\n', kmin);

% 定义减速带输入函数
function y = road_input(t, v)
    y = zeros(size(t));
    t1 = 0.1 / v;   % 上升段结束时间
    t2 = 0.2 / v;   % 平台段结束时间
    t3 = 0.3 / v;   % 下降段结束时间
    
    for i = 1:length(t)
        ti = t(i);
        if ti <= t1
            y(i) = v * ti / 2;
        elseif ti > t1 && ti <= t2
            y(i) = 0.05;
        elseif ti > t2 && ti <= t3
            y(i) = 0.05 - (v * ti - 0.2) / 2;
        else
            y(i) = 0;
        end
    end
end

% 时间向量和车速
t = 0:0.001:2;
v = 5;  % 车速(m/s)

% 生成输入位移曲线
xt = road_input(t, v);

% 定义k值范围
k_values = kmin:5000:200000;

% 预存储所有响应的最大位移值
max_displacements = zeros(size(k_values));
responses = cell(length(k_values), 1);  % 存储所有响应

% 计算所有k值的响应并记录最大位移
for i = 1:length(k_values)
    k = k_values(i);
    SYS = (c*s + k) / (m*s^2 + c*s + k);
    yt = lsim(SYS, xt, t);
    responses{i} = yt;
    max_displacements(i) = max(abs(yt));  % 记录最大绝对值位移
end

% 找到最大位移最小的k值索引
[~, best_idx] = min(max_displacements);
best_k = k_values(best_idx);
fprintf('最大位移最小的刚度: k = %.0f N/m, 对应最大位移: %.4f m\n', best_k, max_displacements(best_idx));

% 初始化图形
figure('Name','不同刚度k对应的系统响应总值','Position',[100 100 1000 600]);
hold on; grid on;
xlabel('时间 t (s)');
ylabel('位移响应 (m)');
title('不同悬挂刚度k的系统位移响应对比 (最大位移最小的刚度用粗线显示)');

% 循环绘制所有响应，最佳k值用粗线
for i = 1:length(k_values)
    k = k_values(i);
    yt = responses{i};
    % 判断是否为最佳k值，设置不同线宽
    if i == best_idx
        plot(t, yt, 'LineWidth', 3, 'DisplayName', sprintf('最佳k = %.0f N/m', k));
    else
        plot(t, yt, 'LineWidth', 1, 'DisplayName', sprintf('k = %.0f N/m', k));
    end
end

% 绘制输入位移曲线和约束线
plot(t, xt, 'k--', 'LineWidth', 1.5, 'DisplayName', '减速带输入');
plot(t, dmax*ones(size(t)), 'r-.', 'LineWidth', 1, 'DisplayName', '最大位移约束');
plot(t, -dmax*ones(size(t)), 'r-.', 'LineWidth', 1);

legend('Location', 'bestoutside');
ylim([-0.3 0.3]);
hold off;
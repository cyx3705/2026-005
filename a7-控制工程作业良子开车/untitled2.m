clc; clear; close all;

s = tf('s');
% 系统基础参数（固定k=135000，聚焦阻尼比分析）
m = 300;         % 车轮等效质量 (kg)
dmax = 0.2;      % 最大刚性位移约束 (m)
k_fixed = 20000;% 固定最小k值（按需求设定）
v = 5;           % 车速(m/s)
kmin = 10*m / dmax;  % 最小刚度约束（仅计算展示，不参与后续）
fprintf('最小刚度约束: kmin = %.2f N/m\n', kmin);
fprintf('当前分析固定刚度: k = %.0f N/m\n', k_fixed);

% 定义减速带输入函数（保持原逻辑）
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

% 时间向量（延长至3秒，确保能观察1秒后震荡是否停止）
t = 0:0.001:3;
% 生成减速带输入位移曲线
xt = road_input(t, v);

% 1. 定义阻尼比ζ范围与对应c值计算
zeta_range = 0:0.02:1;  % ζ从0到1，步长0.02（密度足够观察细节）
c_values = zeta_range * 2 * sqrt(m * k_fixed);  % 按公式c=ζ·2√(mk)计算c
% 预存储响应曲线和震荡停止时间
responses = cell(length(zeta_range), 1);
osc_stop_time = zeros(length(zeta_range), 1);  % 存储各ζ的震荡停止时间

% 2. 计算不同ζ对应的系统响应与震荡停止时间
for i = 1:length(zeta_range)
    zeta = zeta_range(i);
    c = c_values(i);
    
    % 系统传递函数（移除10*m/k项，按需求调整）
    sys = (c*s + k_fixed) / (m*s^2 + c*s + k_fixed);
    yt = lsim(sys, xt, t);  % 计算位移响应
    responses{i} = yt;
    
    % 定义震荡停止判定规则：连续0.1秒（100个采样点）位移绝对值≤0.005m
    threshold = 0.0001;  % 位移阈值（可根据需求微调）
    window_len = 100;   % 0.1秒对应的采样点数量（t步长0.001s）
    stop_flag = false;  % 停止标记
    
    % 从t=0.5秒开始判断（跳过输入激励阶段，聚焦自由震荡）
    start_idx = find(t >= 0.5, 1);
    for j = start_idx:(length(t)-window_len+1)
        % 检查当前窗口内所有位移是否满足停止条件
        if all(abs(yt(j:j+window_len-1)) <= threshold)
            osc_stop_time(i) = t(j);  % 记录停止时间
            stop_flag = true;
            break;
        end
    end
    % 若观察时间内未停止，标记为NaN
    if ~stop_flag
        osc_stop_time(i) = NaN;
    end
end

% 3. 筛选“1秒内震荡停止”的最小ζ与对应c
valid_idx = find(osc_stop_time <= 1);  % 找到满足条件的ζ索引
if ~isempty(valid_idx)
    min_zeta_idx = valid_idx(1);  % 最小ζ对应的索引（第一个满足条件的）
    min_zeta = zeta_range(min_zeta_idx);
    min_c = c_values(min_zeta_idx);
    min_stop_time = osc_stop_time(min_zeta_idx);
    % 命令行输出关键结果
    fprintf('\n满足“1秒内震荡基本停止”的最小参数：\n');
    fprintf('最小阻尼比 ζ = %.2f\n', min_zeta);
    fprintf('对应最小阻尼系数 c = %.2f N·s/m\n', min_c);
    fprintf('实际震荡停止时间 = %.3f s\n', min_stop_time);
else
    fprintf('\n在ζ=0~1范围内，未找到能在1秒内停止震荡的参数（可尝试调小位移阈值）\n');
    min_zeta_idx = [];  % 无满足条件时置空
end

% 4. 绘制位移响应对比图
figure('Name','不同阻尼比ζ的位移响应','Position',[100 100 1200 700]);
hold on; grid on;
xlabel('时间 t (s)','FontSize',11);
ylabel('位移响应 y (m)','FontSize',11);
title(sprintf('固定k=%.0f N/m时，不同阻尼比ζ的系统位移响应\n（红色粗线为1秒内停止的最小ζ）', k_fixed),'FontSize',12);

% 绘制所有ζ的响应曲线（最小ζ用红色粗线突出）
for i = 1:length(zeta_range)
    zeta = zeta_range(i);
    c = c_values(i);
    yt = responses{i};
    if ~isempty(min_zeta_idx) && i == min_zeta_idx
        % 突出显示最小ζ曲线
        plot(t, yt, 'r-', 'LineWidth', 3, ...
            'DisplayName', sprintf('最小ζ=%.2f (c=%.0f N·s/m)', zeta, c));
    else
        % 其他ζ曲线用常规样式
        plot(t, yt, 'LineWidth', 1, ...
            'DisplayName', sprintf('ζ=%.2f (c=%.0f N·s/m)', zeta, c));
    end
end

% 绘制辅助线（输入曲线、位移约束、1秒参考线）
plot(t, xt, 'k--', 'LineWidth', 1.5, 'DisplayName', '减速带输入位移');
plot(t, dmax*ones(size(t)), 'g-.', 'LineWidth', 1, 'DisplayName', '最大位移约束(0.2m)');
plot(t, -dmax*ones(size(t)), 'g-.', 'LineWidth', 1);
plot([1 1], ylim, 'b--', 'LineWidth', 2, 'DisplayName', '1秒判断线');

% 图例与坐标轴设置（避免重叠）
legend('Location', 'bestoutside', 'FontSize', 9);
ylim([-0.3 0.3]);  % 位移范围与原代码保持一致
hold off;

% 5. 绘制“ζ-震荡停止时间”关系图（直观观察规律）
figure('Name','阻尼比ζ与震荡停止时间关系','Position',[200 200 1000 500]);
hold on; grid on;
xlabel('阻尼比 ζ','FontSize',11);
ylabel('震荡停止时间 (s)','FontSize',11);
title(sprintf('固定k=%.0f N/m时，ζ与震荡停止时间的关系\n（红色点为最小ζ，蓝色线为1秒阈值）', k_fixed),'FontSize',12);

% 绘制ζ-停止时间曲线
plot(zeta_range, osc_stop_time, 'b-o', 'LineWidth', 2, 'MarkerSize', 6, 'DisplayName', 'ζ-停止时间');
% 绘制1秒阈值线
plot(zeta_range, ones(size(zeta_range)), 'b--', 'LineWidth', 1.5, 'DisplayName', '1秒阈值');
% 标记最小ζ点
if ~isempty(min_zeta_idx)
    plot(min_zeta, min_stop_time, 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r', ...
        'DisplayName', sprintf('最小ζ=%.2f (c=%.0f)', min_zeta, min_c));
end

legend('Location', 'best', 'FontSize', 10);
xlim([0 1]);  % ζ范围固定0~1
ylim([0 max(osc_stop_time(~isnan(osc_stop_time)))+0.5]);  % 纵轴适配数据
hold off;
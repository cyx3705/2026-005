clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 核心参数配置（固定c，k变化）
% --------------------------
m = 300;                  % 单轮分配的车身等效质量 (kg)
c_fixed = 4135;           % 固定阻尼 (N·s/m)
k_range = 20000:20000:100000;  % 刚度变化范围（20000→100000，步长20000，可调整）
freq_range = [1, 10];     % 扫频范围（Hz），覆盖所有刚度对应的固有频率（~1.3→2.9Hz）
input_amplitude = 0.05;   % 输入振幅（m）

% 计算每个刚度对应的固有频率（用于日志输出）
f_n_list = (1/(2*pi)) * sqrt(k_range/m);
fprintf('各刚度对应的固有频率：\n');
for i = 1:length(k_range)
    fprintf('k=%.0f N/m → 固有频率=%.2f Hz\n', k_range(i), f_n_list(i));
end

% --------------------------
% 2. 创建伯德图（幅频+相频，同一张图）
% --------------------------
figure('Name','不同刚度下的伯德图（c=4135 N·s/m）','Position',[200 200 1200 800]);

% 定义颜色/线型（区分不同刚度）
colors = lines(length(k_range));  % 自动生成区分色
linestyles = {'-', '--', '-.', ':', '-', '--'};  % 线型循环（刚度数量多时自动重复）

% 存储输出振幅最大值（用于适配y轴范围）
max_output_amp = 0;

% 遍历每个刚度，绘制伯德图
for i = 1:length(k_range)
    k_current = k_range(i);
    
    % 系统传递函数（位移响应/路面输入，因果模型：分子1阶≤分母2阶）
    sys = (c_fixed*s + k_current) / (m*s^2 + c_fixed*s + k_current);
    
    % 获取伯德图数据（freq_range转换为rad/s：Hz×2π，适配bode函数）
    [mag, phase, freq] = bode(sys, freq_range*2*pi);  
    mag = squeeze(mag);      % 去除多余维度（1×1×N → N×1）
    phase = squeeze(phase);  % 相位（度）
    freq = squeeze(freq);    % 频率（rad/s）→ 后续转换为Hz
    
    % 幅值转换：dB → 实际输出振幅（元素级运算，避免维度报错）
    mag_ratio = 10.^(mag/20);  % .^ 元素级幂运算（关键修正）
    output_amplitude = mag_ratio * input_amplitude;  % 输出振幅（m）
    
    % 更新最大输出振幅（用于y轴范围适配，避免曲线超出视图）
    if max(output_amplitude) > max_output_amp
        max_output_amp = max(output_amplitude);
    end
    
    % 绘制幅频特性（上子图：输出振幅 vs 频率）
    subplot(2,1,1);
    hold on; grid on;
    plot(freq/(2*pi), output_amplitude, ...  rad/s→Hz，便于阅读
         'Color', colors(i,:), ...
         'LineStyle', linestyles{mod(i-1, length(linestyles))+1}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('k=%.0f N/m', k_current));
    
    % 绘制相频特性（下子图：相位角 vs 频率）
    subplot(2,1,2);
    hold on; grid on;
    plot(freq/(2*pi), phase, ...
         'Color', colors(i,:), ...
         'LineStyle', linestyles{mod(i-1, length(linestyles))+1}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('k=%.0f N/m', k_current));
end

% --------------------------
% 3. 幅频特性图（上子图）美化
% --------------------------
subplot(2,1,1);
xlabel('频率 (Hz)', 'FontSize', 12);
ylabel('输出位移振幅 (m)', 'FontSize', 12);
title(sprintf('不同刚度的幅频特性（c=%d N·s/m，输入振幅=%.02f m）', c_fixed, input_amplitude), 'FontSize', 13);

% 绘制水平参考线（输入振幅0.05m）
yline(input_amplitude, 'Color', 'k', 'LineStyle', ':', 'LineWidth', 1.5, 'Label', '输入振幅=0.05m');
% 绘制垂直参考线（每个刚度对应的固有频率）
for i = 1:length(k_range)
    xline(f_n_list(i), 'Color', colors(i,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(i), f_n_list(i)));
end

legend('Location', 'best', 'FontSize', 9);  % 图例适配，避免遮挡
ylim([0, max_output_amp*1.2]);  % 适配幅值范围，留10%余量

% --------------------------
% 4. 相频特性图（下子图）美化
% --------------------------
subplot(2,1,2);
xlabel('频率 (Hz)', 'FontSize', 12);
ylabel('相位角 (°)', 'FontSize', 12);
title('不同刚度的相频特性', 'FontSize', 13);

% 绘制垂直参考线（每个刚度对应的固有频率，与幅频特性颜色一致）
for i = 1:length(k_range)
    xline(f_n_list(i), 'Color', colors(i,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(i), f_n_list(i)));
end

legend('Location', 'best', 'FontSize', 9);
ylim([-180, 0]);  % 单自由度系统相位范围（0→-180°）

% --------------------------
% 5. 整体布局调整（避免子图重叠）
% --------------------------
sgtitle('单轮系统伯德图（c固定=4135 N·s/m，k变化=20000~100000 N/m）', 'FontSize', 14);
adjustfigsize(gcf);  % 调用自定义函数调整子图间距

% --------------------------
% 自定义函数：调整子图间距（避免未定义函数报错）
% --------------------------
function adjustfigsize(fig)
    % 设置图大小，调整子图位置（left, bottom, width, height）
    set(fig, 'Position', [200 200 1200 800]);
    % 上子图（幅频）位置
    s1 = subplot(2,1,1);
    set(s1, 'Position', [0.12 0.55 0.83 0.4]);  % 左、下、宽、高
    % 下子图（相频）位置
    s2 = subplot(2,1,2);
    set(s2, 'Position', [0.12 0.05 0.83 0.4]);
end
clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 核心参数配置
% --------------------------
m = 300;                  % 单轮分配的车身等效质量 (kg)
k_fixed = 2000;           % 固定刚度 (N/m)
c_range = 3135:1000:8000; % 阻尼变化范围（3135→8000，步长1000）
freq_range = [0.1, 10];   % 扫频范围（Hz），后续转换为rad/s
input_amplitude = 0.05;   % 输入振幅（m）

% 计算系统固有频率（用于图中标注参考）
f_n = (1/(2*pi)) * sqrt(k_fixed/m);
fprintf('系统固有频率：%.2f Hz\n', f_n);

% --------------------------
% 2. 创建伯德图（幅频+相频，同一张图）
% --------------------------
figure('Name','不同阻尼下的伯德图（k=2000 N/m）','Position',[200 200 1200 800]);

% 定义颜色/线型（区分不同阻尼）
colors = lines(length(c_range));  % 自动生成区分色
linestyles = {'-', '--', '-.', ':', '-', '--'};  % 线型循环

% 存储输出振幅最大值（用于适配y轴范围）
max_output_amp = 0;

% 遍历每个阻尼，绘制伯德图
for i = 1:length(c_range)
    c_current = c_range(i);
    
    % 系统传递函数（位移响应/路面输入，因果模型）
    sys = (c_current*s + k_fixed) / (m*s^2 + c_current*s + k_fixed);
    
    % 获取伯德图数据（freq_range需转换为rad/s：Hz×2π）
    [mag, phase, freq] = bode(sys, freq_range*2*pi);  
    mag = squeeze(mag);      % 去除多余维度（1×1×N → N×1）
    phase = squeeze(phase);  % 相位（度）
    freq = squeeze(freq);    % 频率（rad/s）→ 后续转换为Hz
    
    % 幅值转换：dB → 实际输出振幅（元素级运算，避免维度报错）
    mag_ratio = 10.^(mag/20);  % .^ 元素级幂运算
    output_amplitude = mag_ratio * input_amplitude;  % 输出振幅（m）
    
    % 更新最大输出振幅（用于y轴范围适配）
    if max(output_amplitude) > max_output_amp
        max_output_amp = max(output_amplitude);
    end
    
    % 绘制幅频特性（上子图）
    subplot(2,1,1);
    hold on; grid on;
    plot(freq/(2*pi), output_amplitude, ...  rad/s→Hz
         'Color', colors(i,:), ...
         'LineStyle', linestyles{mod(i-1, length(linestyles))+1}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('c=%.0f N·s/m', c_current));
    
    % 绘制相频特性（下子图）
    subplot(2,1,2);
    hold on; grid on;
    plot(freq/(2*pi), phase, ...
         'Color', colors(i,:), ...
         'LineStyle', linestyles{mod(i-1, length(linestyles))+1}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('c=%.0f N·s/m', c_current));
end

% --------------------------
% 3. 幅频特性图（上子图）美化（用MATLAB兼容函数）
% --------------------------
subplot(2,1,1);
xlabel('频率 (Hz)', 'FontSize', 12);
ylabel('输出位移振幅 (m)', 'FontSize', 12);
title(sprintf('不同阻尼的幅频特性（k=%d N/m，输入振幅=%.02f m）', k_fixed, input_amplitude), 'FontSize', 13);

% 绘制水平参考线（MATLAB用yline，替代Python的axhline）
yline(input_amplitude, 'Color', 'k', 'LineStyle', ':', 'LineWidth', 1.5, 'Label', '输入振幅=0.05m');
% 绘制垂直参考线（固有频率）
xline(f_n, 'Color', 'r', 'LineStyle', '--', 'LineWidth', 1.5, 'Label', sprintf('固有频率=%.2f Hz', f_n));

legend('Location', 'best', 'FontSize', 10);
ylim([0, max_output_amp*1.2]);  % 适配幅值范围

% --------------------------
% 4. 相频特性图（下子图）美化
% --------------------------
subplot(2,1,2);
xlabel('频率 (Hz)', 'FontSize', 12);
ylabel('相位角 (°)', 'FontSize', 12);
title('不同阻尼的相频特性', 'FontSize', 13);

% 绘制垂直参考线（固有频率）
xline(f_n, 'Color', 'r', 'LineStyle', '--', 'LineWidth', 1.5, 'Label', sprintf('固有频率=%.2f Hz', f_n));

legend('Location', 'best', 'FontSize', 10);
ylim([-180, 0]);  % 相位范围（单自由度系统0→-180°）

% --------------------------
% 5. 整体布局调整（自定义函数：调整子图间距，避免重叠）
% --------------------------
sgtitle('单轮系统伯德图（k固定=2000 N/m，c变化=3135~8000 N·s/m）', 'FontSize', 14);
adjustfigsize(gcf);  % 调用自定义函数调整布局

% --------------------------
% 自定义函数：调整子图间距（避免未定义函数报错）
% --------------------------
function adjustfigsize(fig)
    set(fig, 'Position', [200 200 1200 800]);
    % 调整子图间距（left, bottom, right, top, wspace, hspace）
    set(gcf, 'Position', [200 200 1200 800]);
    set(gca, 'Position', [0.1 0.1 0.85 0.85]);
    % 子图间距调整
    s = subplot(2,1,1);
    set(s, 'Position', [0.1 0.55 0.85 0.4]);
    s = subplot(2,1,2);
    set(s, 'Position', [0.1 0.05 0.85 0.4]);
end
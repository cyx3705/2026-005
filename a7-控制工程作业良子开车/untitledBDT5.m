clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 核心参数配置（固定k=20000，c步长300，多组数据）
% --------------------------
m = 300;                  % 单轮分配的车身等效质量 (kg)
k_fixed = 20000;          % 固定刚度 (N/m)
c_start = 3135;           % 阻尼起始值 (N·s/m)
c_end = 8000;             % 阻尼结束值 (N·s/m)
c_step = 300;             % 阻尼步长 (N·s/m)
c_range = c_start:c_step:c_end;  % 阻尼变化范围（3135→8000，步长300，共17组）
input_amplitude = 0.05;   % 输入固定振幅 (m)
t = 0:0.001:5;            % 时域时间（仅用于计算加速度，不绘图）
dt = t(2) - t(1);         % 时间步长（0.001s）

% 输入频率：1→3Hz，步长0.08Hz（26个频率，更密集覆盖共振区域）
freq_list = linspace(1, 3, 26);  % 频率密度同步提升，曲线更平滑
num_freq = length(freq_list);    % 频率个数：26个

% 计算系统固有频率（固定k，仅1个固有频率）
f_n = (1/(2*pi)) * sqrt(k_fixed/m);
fprintf('固定刚度k=%.0f N/m → 系统固有频率=%.2f Hz\n', k_fixed, f_n);
fprintf('阻尼变化范围：c=%.0f~%.0f N·s/m（步长=%.0f，共%d组）\n', ...
        c_start, c_end, c_step, length(c_range));

% --------------------------
% 2. 存储核心指标数据（位移幅值+最大加速度）
% --------------------------
displ_amp_all = zeros(length(c_range), num_freq);  % 输出位移幅值（m）
max_acc_all = zeros(length(c_range), num_freq);     % 该频率下的最大加速度（m/s²）

% --------------------------
% 遍历每个阻尼+每个频率，计算核心指标
% --------------------------
for c_idx = 1:length(c_range)
    c_current = c_range(c_idx);
    
    % 系统位移传递函数（因果模型，无阶数溢出）
    sys_displ = (c_current*s + k_fixed) / (m*s^2 + c_current*s + k_fixed);
    
    for freq_idx = 1:num_freq
        f_current = freq_list(freq_idx);  % 当前输入频率
        
        % （1）生成单频输入信号（sin函数，振幅0.05m）
        xt = input_amplitude * sin(2*pi*f_current*t);
        
        % （2）计算位移时域响应（用于求加速度）
        yt_displ = lsim(sys_displ, xt, t);
        
        % （3）计算输出位移幅值（频域线性值×输入振幅）
        [mag, ~, ~] = bode(sys_displ, 2*pi*f_current);  % 幅值比（线性值，输出/输入）
        mag = squeeze(mag);
        displ_amp = mag * input_amplitude;  % 输出位移幅值（m）
        displ_amp_all(c_idx, freq_idx) = displ_amp;
        
        % （4）计算最大加速度（直接二阶差分）
        acc = gradient(gradient(yt_displ, dt), dt);  % 加速度
        max_acc = max(abs(acc));  % 该频率下的最大加速度
        max_acc_all(c_idx, freq_idx) = max_acc;
    end
end

% --------------------------
% 3. 绘制两张核心图（上下布局，优化多组数据区分度）
% --------------------------
figure('Name','悬挂系统幅值-加速度特性图（k=20000 N/m，c步长300）','Position',[200 200 1400 900]);

% 定义颜色映射（用colormap生成17种渐变颜色，区分度更高）
colors = parula(length(c_range));  % 渐变色彩，避免多组数据混淆
linestyles = {'-', '--', '-.', ':'};  % 线型循环（4种线型+渐变颜色，双重区分）

% --------------------------
% 图1：位移幅值特性（输入频率 vs 输出位移幅值）
% --------------------------
subplot(2,1,1);
hold on; grid on;
for c_idx = 1:length(c_range)
    % 线型循环（4种线型重复）
    ls = linestyles{mod(c_idx-1, length(linestyles))+1};
    plot(freq_list, displ_amp_all(c_idx,:), ...
         'Color', colors(c_idx,:), ...
         'LineStyle', ls, ...
         'LineWidth', 1.8, ...
         'DisplayName', sprintf('c=%.0f N·s/m', c_range(c_idx)));
end

% 标注固有频率（红色粗线，突出共振区域）
xline(f_n, 'Color', 'r', 'LineStyle', '--', 'LineWidth', 2.5, ...
      'Label', sprintf('固有频率=%.2f Hz', f_n));
yline(input_amplitude, 'k:', 'LineWidth', 1.5, 'Label', '输入振幅=0.05m');  % 输入参考线

xlabel('输入频率 (Hz)', 'FontSize', 12);
ylabel('输出位移幅值 (m)', 'FontSize', 12);
title(sprintf('不同阻尼的位移幅值特性（k=%.0f N/m，输入振幅=%.02f m，c步长=%.0f）', ...
              k_fixed, input_amplitude, c_step), 'FontSize', 13);
% 图例位置优化（右侧垂直显示，避免遮挡曲线）
legend('Location', 'eastoutside', 'FontSize', 8, 'NumColumns', 1);
ylim([0, max(displ_amp_all(:))*1.2]);  % 适配y轴范围

% --------------------------
% 图2：最大加速度特性（输入频率 vs 最大加速度）
% --------------------------
subplot(2,1,2);
hold on; grid on;
for c_idx = 1:length(c_range)
    ls = linestyles{mod(c_idx-1, length(linestyles))+1};
    plot(freq_list, max_acc_all(c_idx,:), ...
         'Color', colors(c_idx,:), ...
         'LineStyle', ls, ...
         'LineWidth', 1.8, ...
         'DisplayName', sprintf('c=%.0f N·s/m', c_range(c_idx)));
end

% 标注固有频率+舒适性参考线
xline(f_n, 'Color', 'r', 'LineStyle', '--', 'LineWidth', 2.5, ...
      'Label', sprintf('固有频率=%.2f Hz', f_n));
yline(9.8, 'r--', 'LineWidth', 1.5, 'Label', 'g=9.8 m/s²');  % 重力加速度参考线
yline(19.6, 'r:', 'LineWidth', 1.5, 'Label', '2g=19.6 m/s²');  % 舒适性阈值参考线

xlabel('输入频率 (Hz)', 'FontSize', 12);
ylabel('最大加速度 (m/s²)', 'FontSize', 12);
title(sprintf('不同阻尼的最大加速度特性（k=%.0f N/m，输入振幅=%.02f m，c步长=%.0f）', ...
              k_fixed, input_amplitude, c_step), 'FontSize', 13);
legend('Location', 'eastoutside', 'FontSize', 8, 'NumColumns', 1);
ylim([0, max(max_acc_all(:))*1.2]);  % 适配y轴范围

% --------------------------
% 整体布局调整（适配多组图例，避免重叠）
% --------------------------
sgtitle('悬挂系统核心频域特性（固定k=20000 N/m，c=3135~8000 N·s/m，步长300）', 'FontSize', 14);
adjustfigsize(gcf);

% --------------------------
% 自定义函数：调整子图间距（适配右侧图例）
% --------------------------
function adjustfigsize(fig)
    set(fig, 'Position', [200 200 1400 900]);
    % 上子图（位移幅值）：预留右侧图例空间
    set(subplot(2,1,1), 'Position', [0.08 0.55 0.75 0.4]);
    % 下子图（最大加速度）：预留右侧图例空间
    set(subplot(2,1,2), 'Position', [0.08 0.05 0.75 0.4]);
end
clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 核心参数配置（增加k和频率数量，聚焦频域关键指标）
% --------------------------
m = 300;                  % 单轮分配的车身等效质量 (kg)
c_fixed = 4135;           % 固定阻尼 (N·s/m)
k_range = 20000:10000:100000;  % 刚度：20000→100000 N/m（步长10000，共9组）
input_amplitude = 0.05;   % 输入固定振幅 (m)
t = 0:0.001:5;            % 时域时间（仅用于计算加速度，不绘图）
dt = t(2) - t(1);         % 时间步长

% 🔥 增加输入频率数量（1→3Hz，步长0.1Hz，共20个频率，密集覆盖固有频率）
freq_list = linspace(1, 3, 20);  % 频率范围精准覆盖固有频率（1.3~2.9Hz）
num_freq = length(freq_list);    % 频率个数：20个

% 计算每个刚度对应的固有频率（用于图中标注参考）
f_n_list = (1/(2*pi)) * sqrt(k_range/m);
fprintf('各刚度对应的固有频率：\n');
for i = 1:length(k_range)
    fprintf('k=%.0f N/m → 固有频率=%.2f Hz\n', k_range(i), f_n_list(i));
end

% --------------------------
% 2. 存储核心指标数据（位移幅值+最大加速度）
% --------------------------
displ_amp_all = zeros(length(k_range), num_freq);  % 输出位移幅值（m）
max_acc_all = zeros(length(k_range), num_freq);     % 该频率下的最大加速度（m/s²）

% --------------------------
% 遍历每个刚度+每个频率，计算核心指标
% --------------------------
for k_idx = 1:length(k_range)
    k_current = k_range(k_idx);
    
    % 系统位移传递函数（因果模型，无阶数溢出）
    sys_displ = (c_fixed*s + k_current) / (m*s^2 + c_fixed*s + k_current);
    
    for freq_idx = 1:num_freq
        f_current = freq_list(freq_idx);  % 当前输入频率
        
        % （1）生成单频输入信号
        xt = input_amplitude * sin(2*pi*f_current*t);
        
        % （2）计算位移时域响应（用于求加速度）
        yt_displ = lsim(sys_displ, xt, t);
        
        % （3）计算输出位移幅值（频域线性值×输入振幅）
        [mag, ~, ~] = bode(sys_displ, 2*pi*f_current);  % 幅值比（线性值）
        mag = squeeze(mag);
        displ_amp = mag * input_amplitude;  % 输出位移幅值（m）
        displ_amp_all(k_idx, freq_idx) = displ_amp;
        
        % （4）计算最大加速度（Savitzky-Golay平滑+差分，无阶数溢出）
        %yt_smoothed = sgolayfilt(yt_displ, 3, 15);  % 平滑位移信号
        acc = gradient(gradient(yt_displ, dt), dt);  % 加速度
        max_acc = max(abs(acc));  % 该频率下的最大加速度
        max_acc_all(k_idx, freq_idx) = max_acc;
    end
end

% --------------------------
% 3. 绘制两张核心图（上下布局）
% --------------------------
figure('Name','悬挂系统幅值-加速度特性图（c=4135 N·s/m）','Position',[200 200 1200 800]);

% 定义颜色/线型（区分9个刚度，确保区分度）
colors = lines(length(k_range));
linestyles = {'-', '--', '-.', ':', '-', '--', '-.', ':', '-'};  % 循环线型

% --------------------------
% 图1：位移幅值特性（输入频率 vs 输出位移幅值）
% --------------------------
subplot(2,1,1);
hold on; grid on;
for k_idx = 1:length(k_range)
    plot(freq_list, displ_amp_all(k_idx,:), ...
         'Color', colors(k_idx,:), ...
         'LineStyle', linestyles{k_idx}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('k=%.0f N/m', k_range(k_idx)));
    
    % 标注每个刚度的固有频率（垂直参考线）
    xline(f_n_list(k_idx), 'Color', colors(k_idx,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(k_idx), f_n_list(k_idx)));
end
xlabel('输入频率 (Hz)', 'FontSize', 12);
ylabel('输出位移幅值 (m)', 'FontSize', 12);
title('不同刚度的位移幅值特性（输入振幅=0.05m，c=4135 N·s/m）', 'FontSize', 13);
yline(input_amplitude, 'k:', 'LineWidth', 1.5, 'Label', '输入振幅=0.05m');  % 输入参考线
legend('Location', 'best', 'FontSize', 8);  % 图例适配
ylim([0, max(displ_amp_all(:))*1.2]);  % 适配y轴范围

% --------------------------
% 图2：最大加速度特性（输入频率 vs 最大加速度）
% --------------------------
subplot(2,1,2);
hold on; grid on;
for k_idx = 1:length(k_range)
    plot(freq_list, max_acc_all(k_idx,:), ...
         'Color', colors(k_idx,:), ...
         'LineStyle', linestyles{k_idx}, ...
         'LineWidth', 2, ...
         'DisplayName', sprintf('k=%.0f N/m', k_range(k_idx)));
    
    % 标注每个刚度的固有频率（与上图颜色一致）
    xline(f_n_list(k_idx), 'Color', colors(k_idx,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(k_idx), f_n_list(k_idx)));
end
xlabel('输入频率 (Hz)', 'FontSize', 12);
ylabel('最大加速度 (m/s²)', 'FontSize', 12);
title('不同刚度的最大加速度特性（输入振幅=0.05m，c=4135 N·s/m）', 'FontSize', 13);
yline(9.8, 'r--', 'LineWidth', 1.5, 'Label', 'g=9.8 m/s²');  % 重力加速度参考线
yline(19.6, 'r:', 'LineWidth', 1.5, 'Label', '2g=19.6 m/s²');  % 2g参考线（舒适性阈值）
legend('Location', 'best', 'FontSize', 8);
ylim([0, max(max_acc_all(:))*1.2]);  % 适配y轴范围

% --------------------------
% 整体布局调整（避免子图重叠）
% --------------------------
sgtitle('悬挂系统核心频域特性（固定c=4135 N·s/m，k=20000~100000 N/m）', 'FontSize', 14);
adjustfigsize(gcf);

% --------------------------
% 自定义函数：调整子图间距
% --------------------------
function adjustfigsize(fig)
    set(fig, 'Position', [200 200 1200 800]);
    % 上子图（位移幅值）位置
    set(subplot(2,1,1), 'Position', [0.12 0.55 0.83 0.4]);
    % 下子图（最大加速度）位置
    set(subplot(2,1,2), 'Position', [0.12 0.05 0.83 0.4]);
end
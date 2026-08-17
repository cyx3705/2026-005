clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 核心参数配置（固定c，k变化；多个独立单频输入）
% --------------------------
m = 300;                  % 单轮分配的车身等效质量 (kg)
c_fixed = 4135;           % 固定阻尼 (N·s/m)
k_range = 20000:20000:100000;  % 刚度变化范围（20000→100000 N/m）
input_amplitude = 0.05;   % 所有输入的固定振幅 (m)
t = 0:0.001:5;            % 时域时间向量（5秒，足够观察稳态响应）
dt = t(2) - t(1);         % 时间步长

% 🔥 关键：定义多个独立的输入频率（覆盖固有频率1.3~2.9Hz，选5个典型频率）
freq_list = [1, 1.5, 2, 2.5, 3];  % 可按需增加/修改频率，每个频率单独作为输入
num_freq = length(freq_list);     % 输入频率的个数

% 计算每个刚度对应的固有频率（参考）
f_n_list = (1/(2*pi)) * sqrt(k_range/m);
fprintf('各刚度对应的固有频率：\n');
for i = 1:length(k_range)
    fprintf('k=%.0f N/m → 固有频率=%.2f Hz\n', k_range(i), f_n_list(i));
end

% --------------------------
% 2. 创建综合响应图（6个子图：幅频+相频+5个频率的时域波形）
% --------------------------
figure('Name','单频输入-对应频域+时域响应图（c=4135 N·s/m）','Position',[200 200 1600 1200]);

% 定义颜色/线型（区分不同刚度）
colors = lines(length(k_range));
linestyles = {'-', '--', '-.', ':', '-'};

% 存储频域数据（用于绘制幅频/相频图）
mag_all = zeros(length(k_range), num_freq);  % 幅值比（输出/输入）
phase_all = zeros(length(k_range), num_freq);% 相位（度）

% --------------------------
% 第一步：遍历每个刚度，计算所有频率的响应
% --------------------------
for k_idx = 1:length(k_range)
    k_current = k_range(k_idx);
    
    % 系统位移传递函数（分子1阶≤分母2阶，无阶数溢出）
    sys_displ = (c_fixed*s + k_current) / (m*s^2 + c_fixed*s + k_current);
    
    % 遍历每个输入频率，计算对应响应
    for freq_idx = 1:num_freq
        f_current = freq_list(freq_idx);  % 当前输入频率（单频）
        
        % --------------------------
        % （1）生成当前频率的单频输入信号（sin函数，振幅0.05m）
        xt = input_amplitude * sin(2*pi*f_current*t);  % 输入：A*sin(2πft)
        
        % --------------------------
        % （2）计算频域响应（幅值比+相位，即该频率下的伯德图点）
        [mag, phase, ~] = bode(sys_displ, 2*pi*f_current);  % 频率转换为rad/s
        mag = squeeze(mag);          % 幅值比（dB已转换为线性值，输出/输入）
        phase = squeeze(phase);      % 相位（度）
        mag_all(k_idx, freq_idx) = mag;  % 存储幅值比
        phase_all(k_idx, freq_idx) = phase;  % 存储相位
        
        % --------------------------
        % （3）计算时域响应（位移+加速度）
        yt_displ = lsim(sys_displ, xt, t);  % 位移时域响应（稳态正弦波）
        
        % 加速度：Savitzky-Golay平滑+二阶差分（无阶数溢出）
        yt_smoothed = sgolayfilt(yt_displ, 3, 15);
        a_test = gradient(gradient(yt_smoothed, dt), dt);
        
        % --------------------------
        % （4）绘制当前频率的时域响应（每个频率1个子图，共5个）
        subplot(3, 3, freq_idx+2);  % 子图3~7：时域波形（避开幅频/相频子图）
        hold on; grid on;
        % 绘制位移时域响应（主要观察）
        plot(t, yt_displ, ...
             'Color', colors(k_idx,:), 'LineStyle', linestyles{mod(k_idx-1, length(linestyles))+1}, ...
             'LineWidth', 2, 'DisplayName', sprintf('k=%.0f N/m', k_current));
        % 绘制输入信号（灰色细实线，作为参考）
       plot(t, xt, 'Color', [0.5,0.5,0.5], 'LineStyle', '-', 'LineWidth', 1, 'DisplayName', '输入信号');
legend('show');  % 显示图例
        
        % 子图标题+标签（标注当前输入频率）
        title(sprintf('时域响应（输入频率=%.1f Hz）', f_current), 'FontSize', 11);
        xlabel('时间 (s)', 'FontSize', 10);
        ylabel('位移 (m)', 'FontSize', 10);
        legend('Location', 'best', 'FontSize', 8);
        % 适配y轴范围（位移振幅参考）
        ylim([-input_amplitude*2, input_amplitude*2]);
    end
end

% --------------------------
% 第二步：绘制频域响应图（幅频+相频，所有频率的点+拟合曲线）
% --------------------------
% 子图1：幅频特性（所有频率的响应点+拟合曲线）
subplot(3,3,1);
hold on; grid on;
for k_idx = 1:length(k_range)
    % 绘制每个刚度对应的所有频率点
    scatter(freq_list, mag_all(k_idx,:)*input_amplitude, 60, colors(k_idx,:), 'filled', 'MarkerEdgeColor', 'k');
    % 拟合曲线（让频域趋势更清晰）
    plot(freq_list, mag_all(k_idx,:)*input_amplitude, ...
         'Color', colors(k_idx,:), 'LineStyle', linestyles{mod(k_idx-1, length(linestyles))+1}, ...
         'LineWidth', 1.5, 'DisplayName', sprintf('k=%.0f N/m', k_range(k_idx)));
end
xlabel('输入频率 (Hz)', 'FontSize', 11);
ylabel('输出位移振幅 (m)', 'FontSize', 11);
title('幅频特性（每个点对应一个单频输入）', 'FontSize', 12);
yline(input_amplitude, 'k:', 'LineWidth', 1.5, 'Label', '输入振幅=0.05m');
% 标注固有频率参考线
for k_idx = 1:length(k_range)
    xline(f_n_list(k_idx), 'Color', colors(k_idx,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(k_idx), f_n_list(k_idx)));
end
legend('Location', 'best', 'FontSize', 8);

% 子图2：相频特性（所有频率的响应点+拟合曲线）
subplot(3,3,2);
hold on; grid on;
for k_idx = 1:length(k_range)
    % 绘制每个刚度对应的所有频率点
    scatter(freq_list, phase_all(k_idx,:), 60, colors(k_idx,:), 'filled', 'MarkerEdgeColor', 'k');
    % 拟合曲线
    plot(freq_list, phase_all(k_idx,:), ...
         'Color', colors(k_idx,:), 'LineStyle', linestyles{mod(k_idx-1, length(linestyles))+1}, ...
         'LineWidth', 1.5, 'DisplayName', sprintf('k=%.0f N/m', k_range(k_idx)));
end
xlabel('输入频率 (Hz)', 'FontSize', 11);
ylabel('相位角 (°)', 'FontSize', 11);
title('相频特性（每个点对应一个单频输入）', 'FontSize', 12);
% 标注固有频率参考线
for k_idx = 1:length(k_range)
    xline(f_n_list(k_idx), 'Color', colors(k_idx,:), 'LineStyle', '--', 'LineWidth', 1, ...
          'Label', sprintf('k=%.0f→f_n=%.2f Hz', k_range(k_idx), f_n_list(k_idx)));
end
legend('Location', 'best', 'FontSize', 8);
ylim([-180, 0]);

% --------------------------
% 3. 整体布局调整
% --------------------------
sgtitle('单频输入-对应频域+时域响应（c固定=4135 N·s/m，k变化=20000~100000 N/m）', 'FontSize', 14);
adjustfigsize(gcf);

% --------------------------
% 自定义函数：调整子图间距（避免重叠）
% --------------------------
function adjustfigsize(fig)
    set(fig, 'Position', [200 200 1600 1200]);
    % 调整每个子图位置（避免标题/坐标轴重叠）
    subplot(3,3,1); set(gca, 'Position', [0.05 0.7 0.28 0.25]);
    subplot(3,3,2); set(gca, 'Position', [0.38 0.7 0.28 0.25]);
    subplot(3,3,3); set(gca, 'Position', [0.71 0.7 0.28 0.25]);
    subplot(3,3,4); set(gca, 'Position', [0.05 0.4 0.28 0.25]);
    subplot(3,3,5); set(gca, 'Position', [0.38 0.4 0.28 0.25]);
    subplot(3,3,6); set(gca, 'Position', [0.71 0.4 0.28 0.25]);
    subplot(3,3,7); set(gca, 'Position', [0.38 0.1 0.28 0.25]);
end
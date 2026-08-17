clc; clear; close all;
s = tf('s');

% --------------------------
% 1. 系统基础参数（固定关键边界）
% --------------------------
m = 300;          % 车轮等效质量 (kg)
k_min = 135000;   % 最小k值
c_max = 8000;     % 最大c值
v = 5;            % 车速 (m/s)
dmax = 0.2;       % 最大位移约束 (m)
t = 0:0.001:5;    % 延长时间至5s，确保观察完全停止
t3 = 0.3 / v;     % 减速带输入结束时间（0.06s），用于参考

% --------------------------
% 2. 减速带输入函数（保持原逻辑）
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
% 3. 计算Δyₘᵢₙ（k=135000，c=5000）
% --------------------------
c_mid = 5000;
sys_kmin = (c_mid*s + k_min) / (m*s^2 + c_mid*s + k_min);
yt_kmin = lsim(sys_kmin, xt, t);
Delta_y_min = max(abs(yt_kmin));
fprintf('1. k=135000时的最大位移 Δyₘᵢₙ = %.4f m\n', Delta_y_min);

% --------------------------
% 4. 计算tₘᵢₙ（c=8000，k=135000）—— 核心修正
% --------------------------
sys_cmax = (c_max*s + k_min) / (m*s^2 + c_max*s + k_min);
yt_cmax = lsim(sys_cmax, xt, t);

% 修正判定条件：阈值0.001m，从1.0s开始判断
threshold = 0.001;    % 从0.005→0.001m（更严格）
window_len = 100;     % 仍为0.1s窗口
start_idx = find(t >= 0.0, 1);  % 从1.0s开始（避开峰值阶段）
t_min = NaN;
if ~isempty(start_idx)
    for j = start_idx:(length(t)-window_len+1)
        if all(abs(yt_cmax(j:j+window_len-1)) <= threshold)
            t_min = t(j);
            break;
        end
    end
end
if isnan(t_min); t_min = max(t); end  % 未停止则取最大时间
fprintf('2. c=8000时的震荡停止时间 tₘᵢₙ = %.3f s\n', t_min);

% --------------------------
% 5. 重新计算a₁和a₂（基于修正后的tₘᵢₙ）
% --------------------------
if Delta_y_min ~= 0 && t_min ~= 0
    % 场景1：a₁=1（位移权重1）
    a1_sc1 = 1;
    a2_sc1 = - (log10(Delta_y_min) / log10(t_min)) * a1_sc1;
    fprintf('\n【场景1：a₁=1】\n');
    fprintf('a₁=%.4f, a₂=%.4f\n', a1_sc1, a2_sc1);
    fprintf('验证：Δyₘᵢₙ^a1 × tₘᵢₙ^a2 = %.4f（≈1）\n', Delta_y_min^a1_sc1 * t_min^a2_sc1);
    
    % 场景2：a₂=1（时间权重1）
    a2_sc2 = 1;
    a1_sc2 = - (log10(t_min) / log10(Delta_y_min)) * a2_sc2;
    fprintf('\n【场景2：a₂=1】\n');
    fprintf('a₁=%.4f, a₂=%.4f\n', a1_sc2, a2_sc2);
    fprintf('验证：Δyₘᵢₙ^a1 × tₘᵢₙ^a2 = %.4f（≈1）\n', Delta_y_min^a1_sc2 * t_min^a2_sc2);
end

% --------------------------
% 6. 可视化验证（突出修正后的停止时间）
% --------------------------
figure('Name','修正后关键响应','Position',[100 100 1200 600]);
subplot(1,2,1);
plot(t, yt_kmin, 'b-', 'LineWidth', 2);
hold on;
plot(t, xt, 'k--', 'DisplayName', '减速带输入');
plot(t, dmax*ones(size(t)), 'r-.', 'DisplayName', '位移约束');
title(sprintf('k=135000（Δyₘᵢₙ=%.4f m）', Delta_y_min));
xlabel('时间 (s)'); ylabel('位移 (m)'); grid on; legend; ylim([-0.3 0.3]);

subplot(1,2,2);
plot(t, yt_cmax, 'r-', 'LineWidth', 2);
hold on;
plot([t_min t_min], ylim, 'g--', 'DisplayName', sprintf('停止时间=%.3f s', t_min));
plot([1.0 1.0], ylim, 'k-.', 'DisplayName', '开始判断时间(1.0s)');  % 标记判断起点
plot(t, xt, 'k--', 'DisplayName', '减速带输入');
title(sprintf('c=8000（tₘᵢₙ=%.3f s）', t_min));
xlabel('时间 (s)'); ylabel('位移 (m)'); grid on; legend; ylim([-0.01 0.06]);  % 放大纵轴看细节
s = tf('s');
m = 1;
c = 3;
% 定义多个 k 值（示例：k = 0, 1, 2, 3）
k_values = [0, 1, 2, 3,4,5];  

% 时间范围和输入信号
t = 0:0.01:20;
u = ones(size(t));

% 初始化绘图窗口并保持（使曲线叠加）
figure;
hold on;

% 循环绘制每个 k 对应的曲线
for i = 1:length(k_values)
    k = k_values(i);
    % 定义当前 k 对应的传递函数
    G1 = 1 / (m * s^2 + c * s + k);  
    % 计算系统响应
    L = lsim(G1, u, t);  
    % 绘制曲线（可自定义颜色、线型）
    plot(t, L, 'DisplayName', ['k = ' num2str(k)]);  
end

% 添加图例、标签和标题
legend;
xlabel('Time (s)');
ylabel('Response');
title('System Response for Different k Values');
hold off;
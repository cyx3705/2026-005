% 定义传递函数基本参数
s = tf('s');          % 拉普拉斯变量
c = 3;                % 阻尼系数（固定值）
k = 2;                % 刚度系数（固定值）

% 定义多个m值（质量参数），可根据需求修改
m_values = [0.5, 1, 2, 4];  % 不同的质量值

% 时间范围和输入信号
t = 0:0.01:20;             % 时间向量
u = ones(size(t));         % 阶跃输入信号

% 创建图形并开启叠加模式
figure;
hold on;  % 保持当前图形，使新曲线叠加

% 循环绘制每个m值对应的曲线
for i = 1:length(m_values)
    m = m_values(i);                  % 当前质量值
    G = 1 / (m*s^2 + c*s + k);        % 系统传递函数
    [y, t] = lsim(G, u, t);           % 计算系统响应
    % 绘制曲线并设置标签
    plot(t, y, 'LineWidth', 1.2, 'DisplayName', ['m = ' num2str(m)]);
end

% 图形美化
legend('Location', 'best');  % 显示图例
xlabel('时间 (s)');
ylabel('系统响应');
title('不同质量参数 m 对应的系统响应曲线');
grid on;       % 显示网格
box on;        % 显示坐标轴边框
hold off;      % 关闭叠加模式
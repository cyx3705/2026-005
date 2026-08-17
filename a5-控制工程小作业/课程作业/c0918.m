% 定义传递函数的基本参数
s = tf('s');  % 定义拉普拉斯变量
m = 1;        % 质量参数（示例值）
k = 2;        % 刚度参数（示例值）

% 定义多个 c 值（阻尼系数），可根据需要修改
c_values = [1, 3, 5, 7];  % 不同的 c 值

% 时间范围和输入信号
t = 0:0.01:15;            % 时间向量
u = ones(size(t));        % 输入信号（阶跃信号）

% 创建新图并开启叠加模式
figure;
hold on;  % 保持当前图，使新曲线叠加

% 循环绘制每个 c 值对应的曲线
for i = 1:length(c_values)
    c = c_values(i);                  % 当前 c 值
    G = 1 / (m*s^2 + c*s + k);        % 系统传递函数
    [y, t] = lsim(G, u, t);           % 计算系统响应
    plot(t, y, 'LineWidth', 1.2, 'DisplayName', ['c = ' num2str(c)]);  % 绘制曲线
end

% 添加图形装饰
legend('Location', 'best');  % 显示图例，自动选择最佳位置
xlabel('时间 (s)');
ylabel('系统响应');
title('不同阻尼系数 c 对应的系统响应曲线');
grid on;  % 显示网格
hold off; % 关闭叠加模式
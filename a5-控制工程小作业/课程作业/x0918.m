% 定义三个参数的取值（各为1,3,5）
m_values = [1, 3, 5];
c_values = [1, 3, 5];
k_values = [1, 3, 5];

% 时间向量和输入信号
t = 0:0.01:20;  % 时间范围
u = ones(size(t));  % 阶跃输入

% 创建图形并开启叠加模式
figure;
hold on;
grid on;

% 定义不同的线型和颜色，用于区分曲线
lineStyles = {'-', '--', ':'};
colors = {'r', 'g', 'b'};

% 三重循环遍历所有参数组合（m, c, k）
for i = 1:length(m_values)
    m = m_values(i);
    for j = 1:length(c_values)
        c = c_values(j);
        for k = 1:length(k_values)
            k_val = k_values(k);  % 避免与循环变量k冲突
            
            % 计算系统传递函数
            s = tf('s');
            G = 1 / (m*s^2 + c*s + k_val);
            
            % 求解系统响应
            [y, t] = lsim(G, u, t);
            
            % 绘制曲线（用不同样式区分参数）
            plot(t, y, ...
                 'Color', colors{j}, ...  % 用颜色区分c
                 'LineStyle', lineStyles{k}, ...  % 用线型区分k
                 'LineWidth', 1.1, ...
                 'DisplayName', sprintf('m=%d, c=%d, k=%d', m, c, k_val));
        end
    end
end

% 图形美化
legend('Location', 'bestoutside');  % 图例放在图外
xlabel('时间 (s)');
ylabel('系统响应');
title('不同 m, c, k 组合的系统响应曲线');
box on;
hold off;